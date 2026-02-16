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


        public ImageProcessing()
        {
        }
        public void StartProcessing()
        {
            try
            {
                List<string> filePaths = new List<string>();
                if (_mySetings.QueueFolder.Length > 0)
                {
                    // Why the do loop? New files will come in as we are processing. The for each will only get the files for that instant.
                    // The do loop will catch new files as they are comming in so there won't be a processing lag due to the next event timer execution.
                    do
                    {
                        if (Directory.Exists(_mySetings.QueueFolder))
                        {
                            filePaths.Clear();
                            filePaths = Directory.GetFiles(_mySetings.QueueFolder, "*.tif").ToList();
                        }
                        if (filePaths.Count > 0)
                        {
                            if (_myData == null)
                            {
                                _myData = new CommonData();
                            }

                            foreach (string item in filePaths)
                            {
                                try
                                { // Keep trying even if one has an error.
                                    InitializeProcessCache(item);
                                    InsertOrderTicket();
                                }
                                catch (Exception ex)
                                {
                                    err.LogError(ex);
                                }

                            }
                        }
                    } while (Directory.GetFiles(_mySetings.QueueFolder, "*.tif").Count() > 0);
                }
            }
            catch (Exception ex)
            {
                err.LogError(ex);
            }
        }
        private void InitializeProcessCache(string workingPath)
        {
            _processCache = new ProcessCache()
            {
                WorkingFilePath = workingPath,
                ValidationArchiveDirectory = _mySetings.ValidationDirectory,
                MaxUnknownFiles = _mySetings.MaxUnknownFiles,
                QueueFolder = _mySetings.QueueFolder
            };
        }

        private void InsertOrderTicket()
        {
            if (DocumentIsRecognized())
            {
                if (IndexDocumentInDoclink2())
                {
                    if (_processCache.IsSirmProcess)
                    {
                        _processCache.ValidationCompleteDateTime = DateTime.Now.ToString();
                        FileUtilities.WriteValidationXML(ref _processCache);
                    }
                    // Delete the working file. A copy will be in the validation directory as well as archived.
                    // If it fails to be put in doclink the the file will remain. 
                    // The document object was explicitly told not to delete the file upon indexing.
                    if (File.Exists(_processCache.WorkingFilePath))
                    {
                        File.Delete(_processCache.WorkingFilePath);
                    }
                }
            }
            else
            {
                PutDocumentInIndexingFolder();
            }
            //if (Session.IsConnected)
            //{
            //    Session.Logout();
            //}

        }

        /// <summary>
        ///  If you don't want a document to be validated then set the IsSirmProcess bit = 0 in the Common.Document table.
        ///  Currently Invoices are not validated because they come from within the system and are accurately named.
        /// </summary>
        /// <returns></returns>
        private Boolean DocumentIsRecognized()
        {
            Boolean ret = false;
            try
            {
                CommonDataSet.ListSirmDocumentTypeInfoRow dtr = _myData.GetSirmDocumentTypeInfo(_processCache.DocumentType);

                if (dtr != null)
                {
                    _processCache.DL_InitialWorkflowActivityID = dtr.InitialWorkflowActivityID;
                    _processCache.DL_WorkflowID = dtr.WorkflowID;
                    _processCache.DL_WorkFlowQueueID = dtr.WorkflowQueueID;
                    _processCache.DocumentTypeTag = dtr.DocumentTypeTag;
                    _processCache.DL_DocumentTypeID = dtr.DocumentTypeID;
                    _processCache.HasKeyValue = (dtr.HasKeyProperty == 1);

                    if (!dtr.IsKeyPropertyIdNull()) { _processCache.DocumentKeyID = dtr.KeyPropertyId; }

                    if (!dtr.IsDL_TopLevelFolderIDNull()) { _processCache.DL_TopLevelFolder = dtr.DL_TopLevelFolderID; }

                    if (dtr.IsSirmProcessNull())
                    { _processCache.IsSirmProcess = false; }
                    else
                    { _processCache.IsSirmProcess = dtr.SirmProcess; }

                    ret = true;
                }
            }
            catch (Exception ex)
            {
                err.LogError(ex);
            }
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

        private bool IndexDocumentInDoclink2()
        {// This version is to deal with the inclusion of a Trip number associated with a Order Number.
            // To add more indexing items use underscore to delimit. [order number]_[trip]_[next]_[next]-[Doc Type].....
            // ProcessCache.WorkingFilePath is where you parse the string.
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
                Session.Login(auth, _mySetings.Auth_DL_PW);

                // Create the document
                Document doc = new Document();
                doc.BeginEdit();
                doc.DLFolderID = _processCache.DL_TopLevelFolder; 
                doc.DocumentTypeId = _processCache.DL_DocumentTypeID;
                doc.AutoIndexMode = AutoIndexOnDocSaveMode.ExplicitYes;
                doc.CleanUpAddedFilesOnSave = false;
                doc.SetInitialDocumentFile(_processCache.WorkingFilePath);

                // Add property: Output Type
                pv = new PropertyValue();
                pv.PropertyID = _myData.DocumentTypePropertyID; //35; // Maxum doc type identifier - [output Type][sales org]
                dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId;
                pv.Value = _processCache.DocumentType;
                // Cast before Add. SDK requirement, looks odd.
                ipv = pv;
                doc.PropertyValues.Add(ref ipv);

                // Add Key property
                pv = new PropertyValue();
                dtp = new DocumentTypeProperty();
                pv.PropertyID = _processCache.DocumentKeyID; //25 Order number or Invoice etc..
                dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                // If the code fails here you forgot to add 'Document No.' as one of the properties of the document type.
                pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId; //387 
                // If the underlying datatype is other than string you need to explicitly declare it to the Class
                // Then cast Interface to the Class and add the Interface to the PropertyValues collection.
                pv.DataType = new Altec.Biz.Property(_processCache.DocumentKeyID).DataType;
                pv.Value = _processCache.DocumentKeyValue;
                ipv = pv;
                doc.PropertyValues.Add(ref ipv);

                // Add the trip number only if the value exists in the cache
                //if (!string.IsNullOrEmpty(_processCache.TripNumber))
                //{
                //    pv = new PropertyValue();
                //    pv.PropertyID = _myData.TripNumberPropertyID;
                //    dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                //    pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId;
                //    pv.Value = _processCache.TripNumber;
                //    // Cast before Add. 
                //    ipv = pv;
                //    doc.PropertyValues.Add(ref ipv);
                //}

                if (!string.IsNullOrEmpty(_processCache.InvoiceNo))
                {
                    pv = new PropertyValue();
                    pv.PropertyID = _myData.InvoiceNoPropertyID;
                    dtp = doc.DocumentType.DocumentTypeProperties.FindByPropertyId(pv.PropertyID);
                    pv.DocumentTypePropertyId = dtp.DocumentTypePropertyId;
                    pv.Value = _processCache.InvoiceNo;
                    // Cast before Add. 
                    ipv = pv;
                    doc.PropertyValues.Add(ref ipv);
                }


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

        private Boolean PutDocumentInWorkflow(Document doc)
        {
            Boolean ret = false;
            try
            {// version 100.2 added if statement. 12/17/2015 BW
                if (_processCache.DL_WorkflowID > 0)
                {                // Place the document in the initial Status of its workflow.
                    WorkflowQueueDocument wqd = doc.WorkflowQueueDocument;
                    wqd.BeginEdit();
                    wqd.WorkflowQueueID = _processCache.DL_WorkFlowQueueID;
                    wqd.WorkflowId = _processCache.DL_WorkflowID;
                    wqd.WorkflowActivityID = _processCache.DL_InitialWorkflowActivityID;
                    wqd.ApplyEdit();
                }
                ret = true;
            }
            catch (Exception)
            {
            }
            return ret;
        }

        private void PutDocumentInIndexingFolder()
        {
            string location = string.Empty;
            // BW 05/26/2011 New code for SAP ([output code][Sales Org]) Document Codes.
            // The location is defined in the database
            location = _processCache.DocumentType.Replace("XXX", "");
            if (location.Length > 0)
            {
                _processCache.UnknownDirectory = _myData.GetCollatorPath(location);
            }

            if (!Directory.Exists(_processCache.UnknownDirectory))
            {
                _processCache.UnknownDirectory = _mySetings.DefalutUnknownFolder;
            }

            FileUtilities.SetupUnknownFolders(ref _processCache);

            if (_processCache.UnknownDirectory != string.Empty)
            {
                string indexingDirectory = Path.Combine(_processCache.UnknownDirectory, _processCache.UnknownWorkingFolder);
                string saveFileFullName = Path.Combine(indexingDirectory, _processCache.WorkingFile);

                if (!Directory.Exists(indexingDirectory))
                {
                    Directory.CreateDirectory(indexingDirectory);
                }

                // Copy to validate
                if (File.Exists(_processCache.WorkingFilePath))
                {
                    File.Copy(_processCache.WorkingFilePath, saveFileFullName, true);
                }
                // Validate file is writen before delete
                if (File.Exists(saveFileFullName))
                {
                    File.Delete(_processCache.WorkingFilePath);
                    CommonData.SetFileDestination(_processCache.WorkingFilePath, saveFileFullName);
                }
            }
        }

    }
}
