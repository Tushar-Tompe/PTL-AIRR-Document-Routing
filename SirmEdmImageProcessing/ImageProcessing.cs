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
            Logger.Info("Step 3.1: ImageProcessing.StartProcessing initiated.");
            try
            {
                Logger.Info("Step 3.2: Checking QueueFolder configuration.");
                List<string> filePaths = new List<string>();
                if (_mySetings.QueueFolder.Length > 0)
                {
                    // Why the do loop? New files will come in as we are processing. The for each will only get the files for that instant.
                    // The do loop will catch new files as they are comming in so there won't be a processing lag due to the next event timer execution.
                    do
                    {
                        Logger.Info("Step 3.3: Entering file monitoring loop. Scanning QueueFolder: {QueueFolder}", _mySetings.QueueFolder);
                        if (Directory.Exists(_mySetings.QueueFolder))
                        {
                            filePaths.Clear();
                            filePaths = Directory.GetFiles(_mySetings.QueueFolder, "*.tif").ToList();
                            Logger.Info("Step 3.4: Found {FileCount} files in queue. QueueFolder: {QueueFolder}", filePaths.Count, _mySetings.QueueFolder);
                        }
                        if (filePaths.Count > 0)
                        {
                            Logger.Info("Step 3.5: Files found. Preparing to process each file.");
                            if (_myData == null)
                            {
                                Logger.Info("Step 3.5.1: Initializing CommonData instance (database cache).");
                                _myData = new CommonData(); // Steps inside CommonData will be 3.5.1.x
                                Logger.Info("Step 3.5.2: CommonData initialized successfully.");
                            }

                            foreach (string item in filePaths)
                            {
                                using (NLog.ScopeContext.PushProperty("ProcessingFile", item))
                                {
                                    try
                                    { // Keep trying even if one has an error.
                                        Logger.Info("----------------------------------------------------- Processing file: {File} ------------------------------------------------", item);
                                        Logger.Info("Step 3.6: Processing file: {File}", item);

                                        InitializeProcessCache(item); // Step 3.7
                                        InsertOrderTicket();         // Step 3.8

                                        Logger.Info("Step 3.9: Finished processing file: {File}", item);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Warn(ex, "Step 3.10 Error: Error processing file: {File}", item);
                                        err.LogError(ex); // Original error logging
                                    }
                                }

                            }
                        }
                        else
                        {
                            Logger.Info("Step 3.11: No files found in queue. Exiting file monitoring loop.");
                            break; // Exit do-while if no files are found
                        }
                    } while (Directory.GetFiles(_mySetings.QueueFolder, "*.tif").Count() > 0);
                }
                else
                {
                    Logger.Warn("Step 3.12 Warning: QueueFolder setting is empty. No files will be processed.");
                }
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Step 3.13 Fatal: Critical failure in StartProcessing. Processing stopped.");
                err.LogError(ex); // Original error logging
            }
            Logger.Info("Step 3.14: ImageProcessing completed.");
        }

        /// <summary>
        /// Initializes the ProcessCache object for a specific file.
        /// It sets up the file path and various configuration settings for the current processing task.
        /// </summary>
        /// <param name="workingPath">The full file path of the TIFF image to be processed.</param>
        private void InitializeProcessCache(string workingPath)
        {
            Logger.Info("Step 3.7.1: Initializing ProcessCache for working path: {WorkingPath}", workingPath);
            _processCache = new ProcessCache()
            {
                WorkingFilePath = workingPath, // Step 3.7.2 (Setter logic)
                ValidationArchiveDirectory = _mySetings.ValidationDirectory,
                MaxUnknownFiles = _mySetings.MaxUnknownFiles,
                QueueFolder = _mySetings.QueueFolder
            };
            Logger.Info("Step 3.7.3: ProcessCache initialized. ValidationArchiveDirectory: {ValDir}, MaxUnknownFiles: {MaxUnknown}",
                _mySetings.ValidationDirectory, _mySetings.MaxUnknownFiles);
        }

        /// <summary>
        /// Orchestrates the handling of a recognized document.
        /// It attempts to recognize the document type, index it in Doclink, write validation XML, 
        /// and clean up the original file. If recognition fails, it routes the document to an unknown folder.
        /// </summary>
        private void InsertOrderTicket()
        {
            Logger.Info("Step 3.8.1: Starting InsertOrderTicket for file: {WorkingFilePath}", _processCache.WorkingFilePath);
            if (DocumentIsRecognized()) // Step 3.8.2
            {
                Logger.Info("Step 3.8.3: Document type '{DocumentType}' recognized. Proceeding with Doclink indexing.", _processCache.DocumentType);
                if (IndexDocumentInDoclink2()) // Step 3.8.4
                {
                    Logger.Info("Step 3.8.5: Document successfully indexed in Doclink.");
                    if (_processCache.IsSirmProcess)
                    {
                        Logger.Info("Step 3.8.6: Document is SirmProcess. Writing validation XML.");
                        _processCache.ValidationCompleteDateTime = DateTime.Now.ToString();
                        FileUtilities.WriteValidationXML(ref _processCache); // Step 3.8.7
                        Logger.Info("Step 3.8.8: Validation XML written.");
                    }
                    else
                    {
                        Logger.Info("Step 3.8.6: Document is NOT SirmProcess. Skipping validation XML write.");
                    }
                    // Delete the working file. A copy will be in the validation directory as well as archived.
                    // If it fails to be put in doclink the the file will remain. 
                    // The document object was explicitly told not to delete the file upon indexing.
                    if (File.Exists(_processCache.WorkingFilePath))
                    {
                        Logger.Info("Step 3.8.9: Deleting original working file: {WorkingFilePath}", _processCache.WorkingFilePath);
                        File.Delete(_processCache.WorkingFilePath);
                        Logger.Info("Step 3.8.10: Original working file deleted.");
                    }
                    else
                    {
                        Logger.Warn("Step 3.8.9 Warning: Original working file {WorkingFilePath} not found for deletion.", _processCache.WorkingFilePath);
                    }
                }
            }
            else
            {
                Logger.Warn("Step 3.8.13 Warning: Document type '{DocumentType}' not recognized for {WorkingFilePath}. Moving to unknown folder.", _processCache.DocumentType, _processCache.WorkingFilePath);
                PutDocumentInIndexingFolder(); // Step 3.8.14
            }
            Logger.Info("Step 3.8.15: Finished InsertOrderTicket for file: {WorkingFilePath}", _processCache.WorkingFilePath);
        }

        /// <summary>
        /// Determines if the current document type is recognized based on the database cache.
        /// It populates the ProcessCache with Doclink metadata and workflow settings if recognition is successful.
        /// </summary>
        /// <returns>True if the document type is recognized; otherwise, false.</returns>
        private Boolean DocumentIsRecognized()
        {
            Logger.Info("Step 3.8.2.1: Checking if document type '{DocumentType}' is recognized.", _processCache.DocumentType);
            Boolean ret = false;
            try
            {
                Logger.Info("Step 3.8.2.2: Retrieving SirmDocumentTypeInfo for '{DocumentType}'.", _processCache.DocumentType);
                CommonDataSet.ListSirmDocumentTypeInfoRow dtr = _myData.GetSirmDocumentTypeInfo(_processCache.DocumentType); // Step 3.8.2.3

                if (dtr != null)
                {
                    Logger.Info("Step 3.8.2.4: Document type '{DocumentType}' found in cache. Populating ProcessCache.");
                    _processCache.DL_InitialWorkflowActivityID = dtr.InitialWorkflowActivityID;
                    _processCache.DL_WorkflowID = dtr.WorkflowID;
                    _processCache.DL_WorkFlowQueueID = dtr.WorkflowQueueID;
                    _processCache.DocumentTypeTag = dtr.DocumentTypeTag;
                    _processCache.DL_DocumentTypeID = dtr.DocumentTypeID;
                    _processCache.HasKeyValue = (dtr.HasKeyProperty == 1);

                    if (!dtr.IsKeyPropertyIdNull()) { _processCache.DocumentKeyID = dtr.KeyPropertyId; Logger.Info("Step 3.8.2.5: DocumentKeyID set to {KeyID}.", _processCache.DocumentKeyID); }

                    if (!dtr.IsDL_TopLevelFolderIDNull()) { _processCache.DL_TopLevelFolder = dtr.DL_TopLevelFolderID; Logger.Info("Step 3.8.2.6: DL_TopLevelFolder set to {FolderID}.", _processCache.DL_TopLevelFolder); }

                    if (dtr.IsSirmProcessNull())
                    {
                        _processCache.IsSirmProcess = false;
                        Logger.Info("Step 3.8.2.7: IsSirmProcess is null. Defaulting to false for '{DocumentType}'.", _processCache.DocumentType);
                    }
                    else
                    {
                        _processCache.IsSirmProcess = dtr.SirmProcess;
                        Logger.Info("Step 3.8.2.8: IsSirmProcess set to {IsSirmProcess} for '{DocumentType}'.", _processCache.IsSirmProcess, _processCache.DocumentType);
                    }

                    ret = true;
                    Logger.Info("Step 3.8.2.9: Document type '{DocumentType}' successfully recognized.", _processCache.DocumentType);
                }
                else
                {
                    Logger.Warn("Step 3.8.2.10 Warning: Document type '{DocumentType}' not found in database cache.", _processCache.DocumentType);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Step 3.8.2.11 Error: Exception during document type recognition for '{DocumentType}'.", _processCache.DocumentType);
                err.LogError(ex); // Original error logging
            }
            Logger.Info("Step 3.8.2.12: DocumentIsRecognized returning: {Result}", ret);
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
            Logger.Info("Step 3.8.4.1: Starting IndexDocumentInDoclink2 for file: {File} with DocumentType {DocumentType}", _processCache.WorkingFilePath, _processCache.DocumentType);
            Logger.Info("Step 3.8.4.1.1: Initialization Info - DocumentType: {0}, DocumentKey: {1}, InvoiceNo: {2}", 
                _processCache?.DocumentType, _processCache?.DocumentKeyValue, _processCache?.InvoiceNo);
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
                Logger.Info("Step 3.8.4.2: ----------------Attempting to log into Doclink server: {Server} with User: {User} whose Database: {Database} and Doclink EndPoint: {Endpoint}", _mySetings.Auth_DL_Server, _mySetings.Auth_DL_User, _mySetings.Auth_DL_DB, _mySetings.DoclinkEndpoint, _mySetings.Auth_DL_PW);

                Session.Login(auth, _mySetings.Auth_DL_PW);
                Logger.Info("Step 3.8.4.3: Successfully logged into Doclink server: {Server}", _mySetings.Auth_DL_Server);

                // Create the document
                Document doc = new Document();
                doc.BeginEdit();
                Logger.Info("Step 3.8.4.4: Doclink document created and began editing. DocumentId: {DocumentId}", doc.DocumentId);
                doc.DLFolderID = _processCache.DL_TopLevelFolder;
                doc.DocumentTypeId = _processCache.DL_DocumentTypeID;
                doc.AutoIndexMode = AutoIndexOnDocSaveMode.ExplicitYes;
                doc.CleanUpAddedFilesOnSave = false;
                doc.SetInitialDocumentFile(_processCache.WorkingFilePath);
                Logger.Info("Step 3.8.4.5: Basic document properties set. DoclinkFolderID: {FolderID}, DocumentTypeID: {TypeID}", _processCache.DL_TopLevelFolder, _processCache.DL_DocumentTypeID);

                // Add property: Output Type
                pv = new PropertyValue();
                pv.PropertyID = _myData.DocumentTypePropertyID; //35; // Maxum doc type identifier - [output Type][sales org]
                dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                if (dtp != null)
                {
                    pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId;
                    pv.Value = _processCache.DocumentType;
                    ipv = pv;
                    doc.PropertyValues.Add(ref ipv);
                    Logger.Info("Step 3.8.4.6: Added Output Type property (ID: {PropID}) with value: {Value}", pv.PropertyID, _processCache.DocumentType);

                }
                else
                {
                    Logger.Warn("Step 3.8.4.6a: documentTypeProperty is NULL for DocumentTypePropertyID={PropID} (Output Type) in DocumentTypeId={TypeID}. Skipping property.", _myData.DocumentTypePropertyID, _processCache.DL_DocumentTypeID);
                }

                // Add Key property
                pv = new PropertyValue();
                dtp = new DocumentTypeProperty();
                pv.PropertyID = _processCache.DocumentKeyID; //25 Order number or Invoice etc..
                dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                // If the code fails here you forgot to add 'Document No.' as one of the properties of the document type.
                if(dtp != null)
                {
                    pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId; //387 
                    pv.DataType = new Altec.Biz.Property(_processCache.DocumentKeyID).DataType;
                    pv.Value = _processCache.DocumentKeyValue;
                    ipv = pv;
                    doc.PropertyValues.Add(ref ipv);
                    Logger.Info("Step 3.8.4.7: Added Key property (ID: {PropID}) with value: {Value}", pv.PropertyID, _processCache.DocumentKeyValue);

                }
                else
                {
                    Logger.Warn("Step 3.8.4.7a: documentTypeProperty is NULL for DocumentKeyID={PropID} in DocumentTypeId={TypeID}. Document type may be missing 'Document No.' property. Skipping.", _processCache.DocumentKeyID, _processCache.DL_DocumentTypeID);

                }

                if (!string.IsNullOrEmpty(_processCache.InvoiceNo))
                {
                    pv = new PropertyValue();
                    pv.PropertyID = _myData.InvoiceNoPropertyID;
                    dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                    if (dtp != null)
                    {
                        pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId;
                        pv.Value = _processCache.InvoiceNo;
                        ipv = pv;
                        doc.PropertyValues.Add(ref ipv);
                        Logger.Info("Step 3.8.4.8: Added InvoiceNo property (ID: {PropID}) with value: {Value}", pv.PropertyID, _processCache.InvoiceNo);
                    }
                    else
                    {
                        Logger.Warn("Step 3.8.4.8a: documentTypeProperty is NULL for InvoiceNoPropertyID={PropID} in DocumentTypeId={TypeID}",_myData.InvoiceNoPropertyID, _processCache.DL_DocumentTypeID);
                        foreach (DocumentTypeProperty availDtp in doc.DocumentType.DocumentTypeProperties)
                        {
                            Logger.Warn("Step 3.8.4.8b: Available Property -> ID={PropID}, DocTypePropId={DocTypePropId}",availDtp.PropertyId, availDtp.DocumentTypePropertyId);
                        }
                    }
                }

                if (doc.IsValid)
                {
                    Logger.Info("Step 3.8.4.9: Doclink document is valid. Applying edits.");
                    doc.ApplyEdit();
                    _processCache.ValidationDocumentID = doc.DocumentId;
                    Logger.Info("Step 3.8.4.10: Document edits applied. New Doclink DocumentID: {DocumentID}", _processCache.ValidationDocumentID);
                    CommonData.SetFileDestination(_processCache.WorkingFilePath, _processCache.ValidationDocumentID.ToString()); // Step 3.8.4.11
                    Logger.Info("Step 3.8.4.12: Document destination logged for auditing.");

                    if (doc.DocumentId > 0)
                    {
                        Logger.Info("Step 3.8.4.13: Doclink DocumentID is valid. Attempting to put document into workflow.");
                        PutDocumentInWorkflow(doc); // Step 3.8.4.14
                        Logger.Info("Step 3.8.4.15: PutDocumentInWorkflow call completed. Workflow placement may have failed - check Step 3.8.4.14.x warnings above.");
                        ret = true;
                    }
                    else
                    {
                        Logger.Error("Step 3.8.4.16 Error: Doclink did not return a valid DocumentID for {WorkingFilePath}. Throwing exception.", _processCache.WorkingFilePath);
                        throw new InvalidOperationException("The DocumentID was not returned from Doclink. KeyPropertyValue: " + _processCache.DocumentKeyValue.ToString());
                    }
                }
                else
                {
                    Logger.Error("Step 3.8.4.17 Error: Doclink document is NOT valid for {WorkingFilePath}. Validation errors might be present.", _processCache.WorkingFilePath);
                }

            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Step 3.8.4.18 Fatal: Error during Doclink indexing for {WorkingFilePath}.", _processCache.WorkingFilePath);
                err.LogError(ex);
            }
            finally
            {
                if (Session.IsConnected)
                {
                    Session.Logout();
                    Logger.Info("[FINAL] Session logged out");
                }
            }
            Logger.Info("Step 3.8.4.19: IndexDocumentInDoclink2 returning: {Result}", ret);
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
            Logger.Info("Step 3.8.4.14.1: Attempting to put Doclink DocumentID {DocumentID} into workflow.", doc.DocumentId);
            Boolean ret = false;
            WorkflowQueueDocument wqd = null;
            try
            {// version 100.2 added if statement. 12/17/2015 BW
                if (_processCache.DL_WorkflowID > 0)
                {
                    Logger.Info("Step 3.8.4.14.2: WorkflowID {WorkflowID} is valid. Placing document in initial workflow state.", _processCache.DL_WorkflowID);
                    wqd = doc.WorkflowQueueDocument;
                    wqd.BeginEdit();
                    wqd.WorkflowQueueID = _processCache.DL_WorkFlowQueueID;
                    wqd.WorkflowId = _processCache.DL_WorkflowID;
                    wqd.WorkflowActivityID = _processCache.DL_InitialWorkflowActivityID;
                    Logger.Info("-------------------Indexed DOCUMENT OBJECT {wqd}", doc);
                    Logger.Info("-------------------INDEXED WorkFlow DOCUMENT OBJECT {wqd}", wqd);
                    Logger.Info("Step 3.8.4.14.3: Workflow values assigned - QueueID: {QueueID}, WorkflowID: {WorkflowID}, ActivityID: {ActivityID}", 
                        wqd.WorkflowQueueID, wqd.WorkflowId, wqd.WorkflowActivityID);

                    wqd.ApplyEdit();
                    Logger.Info("Step 3.8.4.14.4: DocumentID {DocumentID} successfully placed in workflow {WorkflowID} (Queue: {QueueID}, Activity: {ActivityID}).",
                        doc.DocumentId, _processCache.DL_WorkflowID, _processCache.DL_WorkFlowQueueID, _processCache.DL_InitialWorkflowActivityID);
                }
                else
                {
                    Logger.Info("Step 3.8.4.14.5: DL_WorkflowID is 0. Skipping workflow placement for DocumentID {DocumentID}.", doc.DocumentId);
                }
                ret = true;
            }
            /*catch (Altec.Framework.BizObjectValidationException vex)
            {
                Logger.Warn(vex, "Step 3.8.4.14.6 Warn: Workflow validation failed for DocumentID {DocumentId}. Inspecting broken rules...", doc.DocumentId);
                if (wqd != null && wqd.BrokenRules != null && wqd.BrokenRules.Count > 0)
                {
                    foreach (object rule in wqd.BrokenRules)
                    {
                        // Try to find a property named 'Description' or 'Message' which is common in Altec BrokenRule objects
                        string ruleDetails = rule.ToString();
                        var prop = rule.GetType().GetProperty("Description") ?? rule.GetType().GetProperty("Message");
                        if (prop != null)
                        {
                            ruleDetails = prop.GetValue(rule, null)?.ToString() ?? ruleDetails;
                        }
                        Logger.Warn("Step 3.8.4.14.7 Warn: Broken Rule Details: {Rule}", ruleDetails);
                    }
                }
                throw;
            }*/
            catch (Exception)
            {
                //Logger.Warn(ex,"Step 3.8.4.14.8 Warn: Failed to put document {DocumentId} into workflow.",_processCache?.ValidationDocumentID);
                //throw;
            }
            Logger.Info("Step 3.8.4.14.9: PutDocumentInWorkflow returning: {Result}", ret);
            return ret;
        }

        /// <summary>
        /// Routes an unrecognized document to a specific "unknown" folder based on its document type or location.
        /// It determines the target path, creates necessary subfolders, copies the document, 
        /// and audits the move in the database.
        /// </summary>
        private void PutDocumentInIndexingFolder()
        {
            Logger.Info("Step 3.8.12.1: Document not recognized or Doclink indexing failed. Attempting to move {WorkingFilePath} to unknown folder.", _processCache.WorkingFilePath);
            string location = string.Empty;
            // BW 05/26/2011 New code for SAP ([output code][Sales Org]) Document Codes.
            // The location is defined in the database
            location = _processCache.DocumentType.Replace("XXX", "");
            if (location.Length > 0)
            {
                Logger.Info("Step 3.8.12.2: Getting collator path for location: {Location}", location);
                _processCache.UnknownDirectory = _myData.GetCollatorPath(location); // Step 3.8.12.3
            }

            if (!Directory.Exists(_processCache.UnknownDirectory))
            {
                Logger.Info("Step 3.8.12.4: UnknownDirectory not found. Using default: {DefaultFolder}", _mySetings.DefalutUnknownFolder);
                _processCache.UnknownDirectory = _mySetings.DefalutUnknownFolder;
            }

            Logger.Info("Step 3.8.12.5: Setting up unknown folders.");
            FileUtilities.SetupUnknownFolders(ref _processCache); // Step 3.8.12.6

            if (_processCache.UnknownDirectory != string.Empty)
            {
                string indexingDirectory = Path.Combine(_processCache.UnknownDirectory, _processCache.UnknownWorkingFolder);
                string saveFileFullName = Path.Combine(indexingDirectory, _processCache.WorkingFile);
                Logger.Info("Step 3.8.12.7: Target path determined: {SavePath}", saveFileFullName);

                if (!Directory.Exists(indexingDirectory))
                {
                    Logger.Info("Step 3.8.12.8: Creating indexing directory: {IndexingDir}", indexingDirectory);
                    Directory.CreateDirectory(indexingDirectory);
                }

                // Copy to validate
                if (File.Exists(_processCache.WorkingFilePath))
                {
                    Logger.Info("Step 3.8.12.9: Copying file to unknown folder: {Source} -> {Dest}", _processCache.WorkingFilePath, saveFileFullName);
                    File.Copy(_processCache.WorkingFilePath, saveFileFullName, true);
                }
                // Validate file is writen before delete
                if (File.Exists(saveFileFullName))
                {
                    Logger.Info("Step 3.8.12.10: Copy verified. Deleting original file: {WorkingFilePath}", _processCache.WorkingFilePath);
                    File.Delete(_processCache.WorkingFilePath);
                    CommonData.SetFileDestination(_processCache.WorkingFilePath, saveFileFullName); // Step 3.8.12.11
                    Logger.Info("Step 3.8.12.12: File destination logged.");
                }
            }
            Logger.Info("Step 3.8.12.13: Finished PutDocumentInIndexingFolder.");
        }

    }
}
