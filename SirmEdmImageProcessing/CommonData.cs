using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Maxum.EDM.CommonDataSetTableAdapters;
using NLog;


namespace Maxum.EDM
{

    public class CommonData
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private CommonDataSet.ListSirmDocumentTypeInfoDataTable _documentInfo = null;
        private CommonDataSet.GetLocationCollatorPathsDataTable _collatorInfo = null;
        private CommonDataSet.GetDoclinkPropertysDataTable _docPropertys = null;
        public CommonData()
        {
            _collatorInfo = GetLocationCollatorPaths();
            _documentInfo = ListSirmDocumentTypeInfo();
            _docPropertys = GetDocumentPropertys();

            DocumentTypePropertyID = GetDocumentPropertyIdByTag("SimonsDocumentName");
            TripNumberPropertyID = GetDocumentPropertyIdByTag("TRIP_NUMBER");
            InvoiceNoPropertyID = GetDocumentPropertyIdByTag("InvoiceNo");
        }
        internal int TripNumberPropertyID { get; set; }
        internal int DocumentTypePropertyID { get; set; }
        internal int InvoiceNoPropertyID { get; set; }


        internal CommonDataSet.GetDoclinkPropertysDataTable DocumentPropertys
        {
            get { return _docPropertys; }
            set { _docPropertys = value; }
        }

        internal CommonDataSet.ListSirmDocumentTypeInfoDataTable DocumentInfo
        {
            get { return _documentInfo; }
            set { _documentInfo = value; }
        }

        internal CommonDataSet.GetLocationCollatorPathsDataTable CollatorInfo
        {
            get { return _collatorInfo; }
            set { _collatorInfo = value; }
        }

        internal int GetDocumentPropertyIdByTag(string tag)
        {
            int ret = 0;
            System.Data.DataRow[] r = DocumentPropertys.Select("PropertyTag = '" + tag + "'");
            if (r.Count() > 0)
                ret = ((CommonDataSet.GetDoclinkPropertysRow)r[0]).PropertyId;

            return ret;
        }

        protected internal CommonDataSet.GetDoclinkPropertysDataTable GetDocumentPropertys()
        {
            CommonDataSet.GetDoclinkPropertysDataTable dt = new CommonDataSet.GetDoclinkPropertysDataTable();
            try
            {
                using (GetDoclinkPropertysTableAdapter ta = new GetDoclinkPropertysTableAdapter())
                {
                    ta.Fill(dt);
                }
            }
            catch (Exception) { throw; }
            return dt;
        }

        protected internal CommonDataSet.ListSirmDocumentTypeInfoRow GetSirmDocumentTypeInfo(string documentType)
        {
            CommonDataSet.ListSirmDocumentTypeInfoRow ret = null;
            try
            {
                CommonDataSet.ListSirmDocumentTypeInfoRow[] docTypes;
               System.Data.DataRow[] r = _documentInfo.Select("Name = '" + documentType + "'");

                docTypes = (CommonDataSet.ListSirmDocumentTypeInfoRow[])r;
                // Should only be one record returned.
                if (docTypes != null && docTypes.Length > 0)
                {
                    ret = docTypes[0];
                }

            }
            catch (Exception)
            {

                throw;
            }
            return ret;
        }

        protected internal string GetCollatorPath(string location)
        {
            string ret = string.Empty;
            try
            {
                int iLocation = 0;
                int.TryParse(location, out iLocation);
                if (iLocation != 0)
                {
                    CommonDataSet.GetLocationCollatorPathsRow[] collator;
                    System.Data.DataRow[] r = CollatorInfo.Select("Location = '" + location + "'");
                    collator = (CommonDataSet.GetLocationCollatorPathsRow[])r;
                    if (collator != null && collator.Length > 0)
                    {
                        ret = collator[0].CollatorPath;
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }
            return ret;
        }

        protected internal CommonDataSet.GetLocationCollatorPathsDataTable GetLocationCollatorPaths()
        {
            CommonDataSet.GetLocationCollatorPathsDataTable dt = new CommonDataSet.GetLocationCollatorPathsDataTable();
            try
            {
                using (GetLocationCollatorPathsTableAdapter ta = new GetLocationCollatorPathsTableAdapter())
                {
                    ta.Fill(dt);
                }

            }
            catch (Exception)
            {
                throw;
            }

            return dt;

        }

        protected internal CommonDataSet.ListSirmDocumentTypeInfoDataTable ListSirmDocumentTypeInfo()
        {
            CommonDataSet.ListSirmDocumentTypeInfoDataTable dt = new CommonDataSet.ListSirmDocumentTypeInfoDataTable();
            try
            {
                using (ListSirmDocumentTypeInfoTableAdapter ta = new ListSirmDocumentTypeInfoTableAdapter())
                {
                    ta.Fill(dt);
                }

            }
            catch (Exception)
            {
                throw;

            }

            return dt;

        }

        internal static Boolean SetFileDestination(string fileName, string destination)
        {
            int ret = 1;
            try
            {
                using (QueriesTableAdapter ta = new QueriesTableAdapter())
                {
                    ret = ta.InsertImageIoMessageDestination(fileName, destination);
                }

            }
            catch (Exception)
            {
                // TODO: Create eventing system.
                throw;
            }
            return (ret == 0);

        }

    }
}
