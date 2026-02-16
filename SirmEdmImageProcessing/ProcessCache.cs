using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Maxum.EDM
{
    /// <summary>
    /// The data collected and passed around for processing.
    /// </summary>
    internal class ProcessCache
    {
        #region Private backing vars
        private string _documentType = string.Empty;
        private string _documentKeyValue = string.Empty;
        private string _workingFilepath = string.Empty;
        private string _workingFile = string.Empty;
        private string _tripNumber = string.Empty;
        private string _InvoiceNumber = string.Empty;
        #endregion

        #region Public Properties
        public string DocumentType { get { return _documentType; } set { _documentType = value; } }
        public string TripNumber { get { return _tripNumber; } set { _tripNumber = value; } }
        public string InvoiceNo { get { return _InvoiceNumber; } set { _InvoiceNumber = value; } }
        public string DocumentKeyValue { get { return _documentKeyValue; } set { _documentKeyValue = value; } }
        public string WorkingFile { get { return _workingFile; } set { _workingFile = value; } }
        public string WorkingFilePath
        {
            get { return _workingFilepath; }
            set
            {
                _workingFilepath = value;
                _workingFile = Path.GetFileName(_workingFilepath);
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
                        }
                    }
                    else
                    { _documentKeyValue = s[0]; }

                    _documentType = s[1];
                }
            }
        }
        public int DL_WorkFlowQueueID { get; set; }
        public int DL_WorkflowID { get; set; }
        public int DL_InitialWorkflowActivityID { get; set; }
        public int DL_DocumentTypeID { get; set; }
        public int DL_TopLevelFolder { get; set; }
        public int DocumentKeyID { get; set; }
        public Boolean HasKeyValue { get; set; }
        public Boolean IsSirmProcess { get; set; }
        public string DocumentTypeTag { get; set; }
        public string QueueFolder { get; set; }
        public int MaxUnknownFiles { get; set; }
        public string ValidationArchiveDirectory { get; set; }
        public string ValidationInsertDateTime { get; set; }
        public int ValidationDocumentID { get; set; }
        public string ValidationCompleteDateTime { get; set; }
        public string UnknownDirectory { get; set; }
        public string UnknownWorkingFolder { get; set; }
        public int UnknownFolderFileCount { get; set; }
        #endregion
    }
}
