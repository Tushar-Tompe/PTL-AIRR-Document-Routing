using System;
//using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using System.Text;
//using System.Diagnostics;
using System.IO;
//using Altec.Integration;
using Altec.Biz;
using Altec.Framework;
//using Altec.Framework.ExceptionManagement;
using NLog;
using err = ServiceModelEx.ErrorHandlerHelper;

namespace Maxum.EDM
{
    /// <summary>
    /// 
    /// </summary>
    public class ImageProcessing
    {
        private Properties.Settings _mySetings = Properties.Settings.Default;
        private ProcessCache _processCache;
        private CommonData _myData = null;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


        /// <summary>
        /// Initializes a new instance of the <see cref="ImageProcessing"/> class.
        /// </summary>
        public ImageProcessing()
        {
        }

        /// <summary>
        /// Starts the core image processing workflow.
        /// It monitors the configured queue folder for TIFF files and processes each one sequentially.
        /// This includes initialization of the database cache, file recognition, and document indexing.
        /// </summary>
        public void StartProcessing()
        {
            Logger.Info("Step 1.0: ImageProcessing.StartProcessing initiated.");
            try
            {
                Logger.Info("Step 1.1: Checking QueueFolder configuration.");
                List<string> filePaths = new List<string>();
                if (_mySetings.QueueFolder.Length > 0)
                {
                    // Why the do loop? New files will come in as we are processing. The for each will only get the files for that instant.
                    // The do loop will catch new files as they are comming in so there won't be a processing lag due to the next event timer execution.
                    do
                    {
                        Logger.Info("Step 1.2: Entering file monitoring loop. Scanning QueueFolder: {QueueFolder}", _mySetings.QueueFolder);
                        if (Directory.Exists(_mySetings.QueueFolder))
                        {
                            filePaths.Clear();
                            filePaths = Directory.GetFiles(_mySetings.QueueFolder, "*.tif").ToList();
                            Logger.Info("Step 1.3: Found {FileCount} files in queue. QueueFolder: {QueueFolder}", filePaths.Count, _mySetings.QueueFolder);
                        }
                        if (filePaths.Count > 0)
                        {
                            Logger.Info("Step 1.4: Files found. Preparing to process each file.");
                            if (_myData == null)
                            {
                                Logger.Info("Step 1.5: Initializing CommonData instance (database cache).");
                                _myData = new CommonData(); // SqlException often occurs here
                                Logger.Info("Step 1.6: CommonData initialized successfully.");
                            }

                            foreach (string item in filePaths)
                            {
                                try
                                { // Keep trying even if one has an error.
                                    Logger.Info("Step 1.7: Processing file: {File}", item);

                                    InitializeProcessCache(item);
                                    InsertOrderTicket();

                                    Logger.Info("Step 1.8: Finished processing file: {File}", item);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error(ex, "Step 1.9: Error processing file: {File}", item);
                                    err.LogError(ex); // Original error logging
                                }

                            }
                        }
                        else
                        {
                            Logger.Info("Step 1.10: No files found in queue. Exiting file monitoring loop.");
                            break; // Exit do-while if no files are found
                        }
                    } while (Directory.GetFiles(_mySetings.QueueFolder, "*.tif").Count() > 0);
                }
                else
                {
                    Logger.Warn("Step 1.11: QueueFolder setting is empty. No files will be processed.");
                }
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Step Fatal: Critical failure in StartProcessing. Processing stopped.");
                err.LogError(ex); // Original error logging
            }
            Logger.Info("Step 1.12: ImageProcessing completed.");
        }

        /// <summary>
        /// Initializes the ProcessCache object for a specific file.
        /// It sets up the file path and various configuration settings for the current processing task.
        /// </summary>
        /// <param name="workingPath">The full file path of the TIFF image to be processed.</param>
        private void InitializeProcessCache(string workingPath)
        {
            Logger.Info("Step 2.0: Initializing ProcessCache for working path: {WorkingPath}", workingPath);
            _processCache = new ProcessCache()
            {
                WorkingFilePath = workingPath,
                ValidationArchiveDirectory = _mySetings.ValidationDirectory,
                MaxUnknownFiles = _mySetings.MaxUnknownFiles,
                QueueFolder = _mySetings.QueueFolder
            };
            Logger.Info("Step 2.1: ProcessCache initialized. ValidationArchiveDirectory: {ValDir}, MaxUnknownFiles: {MaxUnknown}",
                _mySetings.ValidationDirectory, _mySetings.MaxUnknownFiles);
        }

        /// <summary>
        /// Orchestrates the handling of a recognized document.
        /// It attempts to recognize the document type, index it in Doclink, write validation XML, 
        /// and clean up the original file. If recognition fails, it routes the document to an unknown folder.
        /// </summary>
        private void InsertOrderTicket()
        {
            Logger.Info("Step 3.0: Starting InsertOrderTicket for file: {WorkingFilePath}", _processCache.WorkingFilePath);
            if (DocumentIsRecognized())
            {
                Logger.Info("Step 3.1: Document type '{DocumentType}' recognized. Proceeding with Doclink indexing.", _processCache.DocumentType);
                if (IndexDocumentInDoclink2())
                {
                    Logger.Info("Step 3.2: Document successfully indexed in Doclink.");
                    if (_processCache.IsSirmProcess)
                    {
                        Logger.Info("Step 3.3: Document is SirmProcess. Writing validation XML.");
                        _processCache.ValidationCompleteDateTime = DateTime.Now.ToString();
                        FileUtilities.WriteValidationXML(ref _processCache);
                        Logger.Info("Step 3.4: Validation XML written.");
                    }
                    else
                    {
                        Logger.Info("Step 3.3: Document is NOT SirmProcess. Skipping validation XML write.");
                    }
                    // Delete the working file. A copy will be in the validation directory as well as archived.
                    // If it fails to be put in doclink the the file will remain. 
                    // The document object was explicitly told not to delete the file upon indexing.
                    if (File.Exists(_processCache.WorkingFilePath))
                    {
                        Logger.Info("Step 3.5: Deleting original working file: {WorkingFilePath}", _processCache.WorkingFilePath);
                        File.Delete(_processCache.WorkingFilePath);
                        Logger.Info("Step 3.6: Original working file deleted.");
                    }
                    else
                    {
                        Logger.Warn("Step 3.5: Original working file {WorkingFilePath} not found for deletion.", _processCache.WorkingFilePath);
                    }
                }
                else
                {
                    Logger.Error("Step 3.7: Document indexing in Doclink failed for {WorkingFilePath}. Moving to unknown folder.", _processCache.WorkingFilePath);
                    PutDocumentInIndexingFolder(); // Fallback to unknown folder if Doclink indexing fails
                }
            }
            else
            {
                Logger.Warn("Step 3.8: Document type '{DocumentType}' not recognized for {WorkingFilePath}. Moving to unknown folder.", _processCache.DocumentType, _processCache.WorkingFilePath);
                PutDocumentInIndexingFolder();
            }
            Logger.Info("Step 3.9: Finished InsertOrderTicket for file: {WorkingFilePath}", _processCache.WorkingFilePath);
        }

        /// <summary>
        /// Determines if the current document type is recognized based on the database cache.
        /// It populates the ProcessCache with Doclink metadata and workflow settings if recognition is successful.
        /// </summary>
        /// <returns>True if the document type is recognized; otherwise, false.</returns>
        private Boolean DocumentIsRecognized()
        {
            Logger.Info("Step 4.0: Checking if document type '{DocumentType}' is recognized.", _processCache.DocumentType);
            Boolean ret = false;
            try
            {
                Logger.Info("Step 4.1: Retrieving SirmDocumentTypeInfo for '{DocumentType}'.", _processCache.DocumentType);
                CommonDataSet.ListSirmDocumentTypeInfoRow dtr = _myData.GetSirmDocumentTypeInfo(_processCache.DocumentType);

                if (dtr != null)
                {
                    Logger.Info("Step 4.2: Document type '{DocumentType}' found in cache. Populating ProcessCache.");
                    _processCache.DL_InitialWorkflowActivityID = dtr.InitialWorkflowActivityID;
                    _processCache.DL_WorkflowID = dtr.WorkflowID;
                    _processCache.DL_WorkFlowQueueID = dtr.WorkflowQueueID;
                    _processCache.DocumentTypeTag = dtr.DocumentTypeTag;
                    _processCache.DL_DocumentTypeID = dtr.DocumentTypeID;
                    _processCache.HasKeyValue = (dtr.HasKeyProperty == 1);

                    if (!dtr.IsKeyPropertyIdNull()) { _processCache.DocumentKeyID = dtr.KeyPropertyId; Logger.Info("Step 4.2.1: DocumentKeyID set to {KeyID}.", _processCache.DocumentKeyID); }

                    if (!dtr.IsDL_TopLevelFolderIDNull()) { _processCache.DL_TopLevelFolder = dtr.DL_TopLevelFolderID; Logger.Info("Step 4.2.2: DL_TopLevelFolder set to {FolderID}.", _processCache.DL_TopLevelFolder); }

                    if (dtr.IsSirmProcessNull())
                    {
                        _processCache.IsSirmProcess = false;
                        Logger.Info("Step 4.2.3: IsSirmProcess is null. Defaulting to false for '{DocumentType}'.", _processCache.DocumentType);
                    }
                    else
                    {
                        _processCache.IsSirmProcess = dtr.SirmProcess;
                        Logger.Info("Step 4.2.4: IsSirmProcess set to {IsSirmProcess} for '{DocumentType}'.", _processCache.IsSirmProcess, _processCache.DocumentType);
                    }

                    ret = true;
                    Logger.Info("Step 4.3: Document type '{DocumentType}' successfully recognized.", _processCache.DocumentType);
                }
                else
                {
                    Logger.Warn("Step 4.4: Document type '{DocumentType}' not found in database cache.", _processCache.DocumentType);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Step Error: Exception during document type recognition for '{DocumentType}'.", _processCache.DocumentType);
                err.LogError(ex); // Original error logging
            }
            Logger.Info("Step 4.5: DocumentIsRecognized returning: {Result}", ret);
            return ret;
        }

        [Obsolete("Use IndexDocumentInDoclink2. Left for sanity check.")]
        private bool IndexDocumentInDoclink()
        {
            bool ret = false;

            IPropertyValue ipv;
            DocumentTypeProperty dtp;
            PropertyValue pv;
            try
            {
                Authorization auth = new Authorization();
                auth.DatabaseName = _mySetings.Auth_DL_DB;
                auth.DatabaseServer = _mySetings.Auth_DL_Server;
                auth.LoginId = _mySetings.Auth_DL_User;
                Altec.Biz.Session.RemotingEndPoint = _mySetings.DoclinkEndpoint;

                Altec.Biz.Session.Login(auth, _mySetings.Auth_DL_PW);

                Document doc = new Document();
                doc.BeginEdit();
                doc.DLFolderID = _processCache.DL_TopLevelFolder; //10030
                //66 - sap delivery ticket 2, others populated from table
                doc.DocumentTypeId = _processCache.DL_DocumentTypeID; 
                doc.AutoIndexMode = AutoIndexOnDocSaveMode.ExplicitYes;
                doc.CleanUpAddedFilesOnSave = false;

                doc.SetInitialDocumentFile(_processCache.WorkingFilePath);

                pv = new PropertyValue();
                pv.PropertyID = 35; // Maxum doc type identifier - [output Type][sales org]
                dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId;
                pv.Value = _processCache.DocumentType;
                // Cast before Add.
                ipv = pv;
                doc.PropertyValues.Add(ref ipv);

                // Add Key property
                pv = new PropertyValue();
                dtp = new DocumentTypeProperty();
                pv.PropertyID = _processCache.DocumentKeyID; //25 Order number


                dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId; //387 
                // If the underlying datatype is other than string you need to explicitly declare it to the Class
                // Then cast Interface to the Class and add the Interface to the PropertyValues collection.
                pv.DataType = new Altec.Biz.Property(_processCache.DocumentKeyID).DataType;
                pv.Value = _processCache.DocumentKeyValue;
                ipv = pv;
                doc.PropertyValues.Add(ref ipv);


                if (doc.IsValid)
                {
                    doc.ApplyEdit();
                    _processCache.ValidationDocumentID = doc.DocumentId;
                    // This is for audit 
                    CommonData.SetFileDestination(_processCache.WorkingFilePath, _processCache.ValidationDocumentID.ToString());

                    if (doc.DocumentId > 0)
                    {
                        PutDocumentInWorkflow(doc);

                        // BW 11/08/2012: Moved this to here. We want to make sure we get a valid DocumentID. Error can happen on ApplyEdit if out of disk space or network error.
                        ret = true;
                    }
                    else
                    {
                        throw new InvalidOperationException("The DocumentID was not returned from Doclink. KeyPropertyValue: " + _processCache.DocumentKeyValue.ToString());
                    }
                }

            }
            catch (Exception ex)
            {
                err.LogError(ex);
            }

            return ret;
        }

        /// <summary>
        /// Indexes the document in the Doclink system.
        /// It performs login, creates a Doclink document object, assigns core and metadata properties,
        /// and attempts to save the document. Upon success, it places the document into its configured workflow.
        /// </summary>
        /// <returns>True if indexing and workflow placement were successful; otherwise, false.</returns>
        private bool IndexDocumentInDoclink2()
        {// This version is to deal with the inclusion of a Trip number associated with a Order Number.
            // To add more indexing items use underscore to delimit. [order number]_[trip]_[next]_[next]-[Doc Type].....
            // ProcessCache.WorkingFilePath is where you parse the string.
            Logger.Info("Step 5.0: Starting IndexDocumentInDoclink2 for file: {File} with DocumentType {DocumentType}", _processCache.WorkingFilePath, _processCache.DocumentType);
            bool ret = false;

            IPropertyValue ipv;
            DocumentTypeProperty dtp;
            PropertyValue pv;
            try
            {//Login
                Authorization auth = new Authorization();
                auth.DatabaseName = _mySetings.Auth_DL_DB;
                auth.DatabaseServer = _mySetings.Auth_DL_Server;
                auth.LoginId = _mySetings.Auth_DL_User;
                Session.RemotingEndPoint = _mySetings.DoclinkEndpoint;
                Logger.Debug("Step 5.1: Attempting to log into Doclink server: {Server}", _mySetings.Auth_DL_Server);
                Session.Login(auth, _mySetings.Auth_DL_PW);
                Logger.Info("Step 5.2: Successfully logged into Doclink server: {Server}", _mySetings.Auth_DL_Server);

                // Create the document
                Document doc = new Document();
                doc.BeginEdit();
                Logger.Info("Step 5.3: Doclink document created and began editing. DocumentId: {DocumentId}", doc.DocumentId);
                doc.DLFolderID = _processCache.DL_TopLevelFolder;
                doc.DocumentTypeId = _processCache.DL_DocumentTypeID;
                doc.AutoIndexMode = AutoIndexOnDocSaveMode.ExplicitYes;
                doc.CleanUpAddedFilesOnSave = false;
                doc.SetInitialDocumentFile(_processCache.WorkingFilePath);
                Logger.Info("Step 5.4: Basic document properties set. DoclinkFolderID: {FolderID}, DocumentTypeID: {TypeID}", _processCache.DL_TopLevelFolder, _processCache.DL_DocumentTypeID);

                // Add property: Output Type
                pv = new PropertyValue();
                pv.PropertyID = _myData.DocumentTypePropertyID; //35; // Maxum doc type identifier - [output Type][sales org]
                dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId;
                pv.Value = _processCache.DocumentType;
                ipv = pv;
                doc.PropertyValues.Add(ref ipv);
                Logger.Info("Step 5.5: Added Output Type property (ID: {PropID}) with value: {Value}", pv.PropertyID, _processCache.DocumentType);

                // Add Key property
                pv = new PropertyValue();
                dtp = new DocumentTypeProperty();
                pv.PropertyID = _processCache.DocumentKeyID; //25 Order number or Invoice etc..
                dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                // If the code fails here you forgot to add 'Document No.' as one of the properties of the document type.
                pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId; //387 
                pv.DataType = new Altec.Biz.Property(_processCache.DocumentKeyID).DataType;
                pv.Value = _processCache.DocumentKeyValue;
                ipv = pv;
                doc.PropertyValues.Add(ref ipv);
                Logger.Info("Step 5.6: Added Key property (ID: {PropID}) with value: {Value}", pv.PropertyID, _processCache.DocumentKeyValue);

                if (!string.IsNullOrEmpty(_processCache.InvoiceNo))
                {
                    pv = new PropertyValue();
                    pv.PropertyID = _myData.InvoiceNoPropertyID;
                    dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                    pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId;
                    pv.Value = _processCache.InvoiceNo;
                    ipv = pv;
                    doc.PropertyValues.Add(ref ipv);
                    Logger.Info("Step 5.7: Added InvoiceNo property (ID: {PropID}) with value: {Value}", pv.PropertyID, _processCache.InvoiceNo);
                }

                if (doc.IsValid)
                {
                    Logger.Info("Step 5.8: Doclink document is valid. Applying edits.");
                    doc.ApplyEdit();
                    _processCache.ValidationDocumentID = doc.DocumentId;
                    Logger.Info("Step 5.9: Document edits applied. New Doclink DocumentID: {DocumentID}", _processCache.ValidationDocumentID);
                    CommonData.SetFileDestination(_processCache.WorkingFilePath, _processCache.ValidationDocumentID.ToString());
                    Logger.Info("Step 5.10: Document destination logged for auditing.");

                    if (doc.DocumentId > 0)
                    {
                        Logger.Info("Step 5.11: Doclink DocumentID is valid. Attempting to put document into workflow.");
                        PutDocumentInWorkflow(doc);
                        Logger.Info("Step 5.12: Document successfully placed in workflow.");
                        ret = true;
                    }
                    else
                    {
                        Logger.Error("Step 5.13: Doclink did not return a valid DocumentID for {WorkingFilePath}. Throwing exception.", _processCache.WorkingFilePath);
                        throw new InvalidOperationException("The DocumentID was not returned from Doclink. KeyPropertyValue: " + _processCache.DocumentKeyValue.ToString());
                    }
                }
                else
                {
                    Logger.Error("Step 5.14: Doclink document is NOT valid for {WorkingFilePath}. Validation errors might be present.", _processCache.WorkingFilePath);
                }

            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Step Fatal: Error during Doclink indexing for {WorkingFilePath}.", _processCache.WorkingFilePath);
                err.LogError(ex);
            }

            Logger.Info("Step 5.15: IndexDocumentInDoclink2 returning: {Result}", ret);
            return ret;
        }

        /// <summary>
        /// Places an indexed Doclink document into its initial workflow state.
        /// It sets the workflow ID, queue ID, and activity ID on the document's workflow object.
        /// </summary>
        /// <param name="doc">The Doclink document object to be placed in workflow.</param>
        /// <returns>True if the document was successfully placed in workflow; otherwise, false.</returns>
        private Boolean PutDocumentInWorkflow(Document doc)
        {
            Logger.Info("Step 6.0: Attempting to put Doclink DocumentID {DocumentID} into workflow.", doc.DocumentId);
            Boolean ret = false;
            try
            {// version 100.2 added if statement. 12/17/2015 BW
                if (_processCache.DL_WorkflowID > 0)
                {
                    Logger.Info("Step 6.1: WorkflowID {WorkflowID} is valid. Placing document in initial workflow state.", _processCache.DL_WorkflowID);
                    WorkflowQueueDocument wqd = doc.WorkflowQueueDocument;
                    wqd.BeginEdit();
                    wqd.WorkflowQueueID = _processCache.DL_WorkFlowQueueID;
                    wqd.WorkflowId = _processCache.DL_WorkflowID;
                    wqd.WorkflowActivityID = _processCache.DL_InitialWorkflowActivityID;
                    wqd.ApplyEdit();
                    Logger.Info("Step 6.2: DocumentID {DocumentID} successfully placed in workflow {WorkflowID} (Queue: {QueueID}, Activity: {ActivityID}).",
                        doc.DocumentId, _processCache.DL_WorkflowID, _processCache.DL_WorkFlowQueueID, _processCache.DL_InitialWorkflowActivityID);
                }
                else
                {
                    Logger.Info("Step 6.3: DL_WorkflowID is 0. Skipping workflow placement for DocumentID {DocumentID}.", doc.DocumentId);
                }
                ret = true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex,"Step Error: Failed to put document {DocumentId} into workflow.",_processCache?.ValidationDocumentID);
                throw;
            }
            Logger.Info("Step 6.4: PutDocumentInWorkflow returning: {Result}", ret);
            return ret;
        }

        /// <summary>
        /// Routes an unrecognized document to a specific "unknown" folder based on its document type or location.
        /// It determines the target path, creates necessary subfolders, copies the document, 
        /// and audits the move in the database.
        /// </summary>
        private void PutDocumentInIndexingFolder()
        {
            Logger.Info("Step 7.0: Document not recognized or Doclink indexing failed. Attempting to move {WorkingFilePath} to unknown folder.", _processCache.WorkingFilePath);
            string location = string.Empty;
            // BW 05/26/2011 New code for SAP ([output code][Sales Org]) Document Codes.
            // The location is defined in the database
            location = _processCache.DocumentType.Replace("XXX", "");
            if (location.Length > 0)
            {
                Logger.Info("Step 7.1: Getting collator path for location: {Location}", location);
                _processCache.UnknownDirectory = _myData.GetCollatorPath(location);
            }

            if (!Directory.Exists(_processCache.UnknownDirectory))
            {
                Logger.Info("Step 7.2: UnknownDirectory not found. Using default: {DefaultFolder}", _mySetings.DefalutUnknownFolder);
                _processCache.UnknownDirectory = _mySetings.DefalutUnknownFolder;
            }

            Logger.Info("Step 7.3: Setting up unknown folders.");
            FileUtilities.SetupUnknownFolders(ref _processCache);

            if (_processCache.UnknownDirectory != string.Empty)
            {
                string indexingDirectory = Path.Combine(_processCache.UnknownDirectory, _processCache.UnknownWorkingFolder);
                string saveFileFullName = Path.Combine(indexingDirectory, _processCache.WorkingFile);
                Logger.Info("Step 7.4: Target path determined: {SavePath}", saveFileFullName);

                if (!Directory.Exists(indexingDirectory))
                {
                    Logger.Info("Step 7.5: Creating indexing directory: {IndexingDir}", indexingDirectory);
                    Directory.CreateDirectory(indexingDirectory);
                }

                // Copy to validate
                if (File.Exists(_processCache.WorkingFilePath))
                {
                    Logger.Info("Step 7.6: Copying file to unknown folder: {Source} -> {Dest}", _processCache.WorkingFilePath, saveFileFullName);
                    File.Copy(_processCache.WorkingFilePath, saveFileFullName, true);
                }
                // Validate file is writen before delete
                if (File.Exists(saveFileFullName))
                {
                    Logger.Info("Step 7.7: Copy verified. Deleting original file: {WorkingFilePath}", _processCache.WorkingFilePath);
                    File.Delete(_processCache.WorkingFilePath);
                    CommonData.SetFileDestination(_processCache.WorkingFilePath, saveFileFullName);
                    Logger.Info("Step 7.8: File destination logged.");
                }
            }
            Logger.Info("Step 7.9: Finished PutDocumentInIndexingFolder.");
        }

    }
}
