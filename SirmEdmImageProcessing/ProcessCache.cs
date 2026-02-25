using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Maxum.EDM
{
    /// <summary>
    /// Represents a cache for storing and managing data collected and passed around during the image processing workflow.
    /// This includes file paths, document metadata, Doclink IDs, and various configuration settings.
    /// </summary>
    internal class ProcessCache
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        #region Private backing vars
        private string _documentType = string.Empty;
        private string _documentKeyValue = string.Empty;
        private string _workingFilepath = string.Empty;
        private string _workingFile = string.Empty;
        private string _tripNumber = string.Empty;
        private string _InvoiceNumber = string.Empty;
        #endregion

        #region Public Properties
        /// <summary>
        /// Gets or sets the document type extracted from the filename.
        /// </summary>
        public string DocumentType { get { return _documentType; } set { _documentType = value; } }
        /// <summary>
        /// Gets or sets the trip number extracted from the filename.
        /// </summary>
        public string TripNumber { get { return _tripNumber; } set { _tripNumber = value; } }
        /// <summary>
        /// Gets or sets the invoice number extracted from the filename.
        /// </summary>
        public string InvoiceNo { get { return _InvoiceNumber; } set { _InvoiceNumber = value; } }
        /// <summary>
        /// Gets or sets the key value (e.g., order number) extracted from the filename.
        /// </summary>
        public string DocumentKeyValue { get { return _documentKeyValue; } set { _documentKeyValue = value; } }
        /// <summary>
        /// Gets or sets the name of the working file (without path).
        /// </summary>
        public string WorkingFile { get { return _workingFile; } set { _workingFile = value; } }
        /// <summary>
        /// Gets or sets the full path to the working file.
        /// Setting this property also triggers parsing of the filename to extract document type, key value, and invoice number.
        /// </summary>
        public string WorkingFilePath
        {
            get { return _workingFilepath; }
            set
            {
                _workingFilepath = value;
                Logger.Info("Step 19.0: Setting WorkingFilePath to: '{0}'", value);
                _workingFile = Path.GetFileName(_workingFilepath);
                Logger.Info("Step 19.1: Extracted WorkingFile: '{0}' from path.", _workingFile);
                string[] s = _workingFile.Split('-');
                if (s.Length > 1)
                {
                    // The underscore is used to divide SO from Trip. If more extended data is needed use additional underscores. Code for them. Update ImageProcessing::IndexDocumentInDoclink2
                    if (s[0].Contains("_"))
                    {
                        string[] v = s[0].Split('_');
                        if (v.Length > 1)
                        {
                            _documentKeyValue = v[0];
                            //_tripNumber = v[1];
                            _InvoiceNumber = v[1];
                            Logger.Info("Step 19.2: Parsed KeyValue: '{0}', InvoiceNo: '{1}' from '{2}'.", _documentKeyValue, _InvoiceNumber, s[0]);
                        }
                    }
                    else
                    {
                        Logger.Warn("Step 19.3 Warn: Malformed filename segment in '{0}'. Expected underscore separation for KeyValue and InvoiceNo. Full filename: '{1}'. Assigning '{0}' to KeyValue.", s[0], _workingFile);
                        _documentKeyValue = s[0]; 
                    }

                    _documentType = s[1];
                    Logger.Info("Step 19.4: Parsed DocumentType: '{0}'.", _documentType);
                }
                else
                {
                    Logger.Warn("Step 19.5 Warn: Filename '{0}' does not conform to expected 'KeyValue-DocType-GUID' format.", _workingFile);
                }
                Logger.Info("Step 19.6: WorkingFilePath setter logic completed.");
            }
        }
        public int DL_WorkFlowQueueID { get; set; }
        /// <summary>
        /// Gets or sets the Doclink Workflow ID.
        /// </summary>
        public int DL_WorkflowID { get; set; }
        /// <summary>
        /// Gets or sets the Doclink Initial Workflow Activity ID.
        /// </summary>
        public int DL_InitialWorkflowActivityID { get; set; }
        /// <summary>
        /// Gets or sets the Doclink Document Type ID.
        /// </summary>
        public int DL_DocumentTypeID { get; set; }
        /// <summary>
        /// Gets or sets the Doclink Top Level Folder ID.
        /// </summary>
        public int DL_TopLevelFolder { get; set; }
        /// <summary>
        /// Gets or sets the Doclink Document Key ID.
        /// </summary>
        public int DocumentKeyID { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the document has a key-value property.
        /// </summary>
        public Boolean HasKeyValue { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether the document is part of the SIRM process.
        /// </summary>
        public Boolean IsSirmProcess { get; set; }
        /// <summary>
        /// Gets or sets the document type tag.
        /// </summary>
        public string DocumentTypeTag { get; set; }
        /// <summary>
        /// Gets or sets the folder where new documents are queued for processing.
        /// </summary>
        public string QueueFolder { get; set; }
        /// <summary>
        /// Gets or sets the maximum number of unknown files allowed in a folder before a new one is created.
        /// </summary>
        public int MaxUnknownFiles { get; set; }
        /// <summary>
        /// Gets or sets the directory where validation archives are stored.
        /// </summary>
        public string ValidationArchiveDirectory { get; set; }
        /// <summary>
        /// Gets or sets the date and time when validation data was inserted.
        /// </summary>
        public string ValidationInsertDateTime { get; set; }
        /// <summary>
        /// Gets or sets the DocumentID assigned during validation.
        /// </summary>
        public int ValidationDocumentID { get; set; }
        /// <summary>
        /// Gets or sets the date and time when validation was completed.
        /// </summary>
        public string ValidationCompleteDateTime { get; set; }
        /// <summary>
        /// Gets or sets the base directory for unknown files.
        /// </summary>
        public string UnknownDirectory { get; set; }
        /// <summary>
        /// Gets or sets the specific working folder within the unknown directory.
        /// </summary>
        public string UnknownWorkingFolder { get; set; }
        /// <summary>
        /// Gets or sets the count of files in the current unknown working folder.
        /// </summary>
        public int UnknownFolderFileCount { get; set; }
        #endregion
    }
}
