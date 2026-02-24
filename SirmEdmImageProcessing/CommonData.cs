using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Maxum.EDM.CommonDataSetTableAdapters;
using NLog;


namespace Maxum.EDM
{
    /// <summary>
    /// Provides common data access and caching functionalities for SIRM
    /// and Doclink integrations. This class is responsible for retrieving and caching Doclink properties,
    /// document type information, and collator paths from the database.
    /// </summary>
    public class CommonData
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private CommonDataSet.ListSirmDocumentTypeInfoDataTable _documentInfo = null;
        private CommonDataSet.GetLocationCollatorPathsDataTable _collatorInfo = null;
        private CommonDataSet.GetDoclinkPropertysDataTable _docPropertys = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommonData"/> class.
        /// It populates the in-memory cache by fetching location collator paths, SIRM document type info, 
        /// and Doclink properties from the database. It also maps specific Doclink Property IDs by their tags.
        /// </summary>
        public CommonData()
        {
            Logger.Info("Step 8.0: Initializing CommonData instance.");
            try
            {
                Logger.Info("Step 8.1: Fetching location collator paths.");
                _collatorInfo = GetLocationCollatorPaths();
                Logger.Info("Step 8.2: Fetching SIRM document type info.");
                _documentInfo = ListSirmDocumentTypeInfo();
                Logger.Info("Step 8.3: Fetching Doclink properties.");
                _docPropertys = GetDocumentPropertys();

                Logger.Info("Step 8.4: Mapping specific property IDs by tag.");
                DocumentTypePropertyID = GetDocumentPropertyIdByTag("SimonsDocumentName");
                TripNumberPropertyID = GetDocumentPropertyIdByTag("TRIP_NUMBER");
                InvoiceNoPropertyID = GetDocumentPropertyIdByTag("InvoiceNo");

                Logger.Info("Step 8.5: CommonData initialized successfully.");

            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Step Fatal: Failed to initialize CommonData during constructor execution.");
                throw;
            }

            }
        /// <summary>
        /// Gets or sets the Doclink Property ID for the Trip Number.
        /// </summary>
        internal int TripNumberPropertyID { get; set; }
        /// <summary>
        /// Gets or sets the Doclink Property ID for the Document Type.
        /// </summary>
        internal int DocumentTypePropertyID { get; set; }
        /// <summary>
        /// Gets or sets the Doclink Property ID for the Invoice Number.
        /// </summary>
        internal int InvoiceNoPropertyID { get; set; }


        /// <summary>
        /// Gets or sets the cached DataTable containing Doclink properties.
        /// </summary>
        internal CommonDataSet.GetDoclinkPropertysDataTable DocumentPropertys
        {
            get { return _docPropertys; }
            set { _docPropertys = value; }
        }

        /// <summary>
        /// Gets or sets the cached DataTable containing SIRM document type information.
        /// </summary>
        internal CommonDataSet.ListSirmDocumentTypeInfoDataTable DocumentInfo
        {
            get { return _documentInfo; }
            set { _documentInfo = value; }
        }

        /// <summary>
        /// Gets or sets the cached DataTable containing location collator paths.
        /// </summary>
        internal CommonDataSet.GetLocationCollatorPathsDataTable CollatorInfo
        {
            get { return _collatorInfo; }
            set { _collatorInfo = value; }
        }

        /// <summary>
        /// Retrieves the Doclink PropertyId for a given property tag string.
        /// It searches the cached Doclink properties table for a matching tag.
        /// </summary>
        /// <param name="tag">The property tag string to search for.</param>
        /// <returns>The numerical Doclink PropertyId if found; otherwise, 0.</returns>
        internal int GetDocumentPropertyIdByTag(string tag)
        {
            int ret = 0;
            Logger.Debug("Step 9.0: Fetching PropertyId for tag: {Tag}", tag);
            if (string.IsNullOrWhiteSpace(tag))
            {
                Logger.Warn("Step 9.1: Tag is null or empty. Cannot fetch PropertyId.");
                return ret;
            }
            try
            {
                System.Data.DataRow[] r = DocumentPropertys.Select("PropertyTag = '" + tag + "'");
                if (r.Count() > 0)
                {
                    ret = ((CommonDataSet.GetDoclinkPropertysRow)r[0]).PropertyId;
                    Logger.Debug("Step 9.2: Found PropertyId {PropertyId} for tag '{Tag}'", ret, tag);
                    return ret;
                }
                Logger.Warn("Step 9.3: No PropertyId found in cache for tag '{Tag}'", tag);
                return ret;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Step Error: Error retrieving PropertyId for tag '{Tag}'", tag);
                throw;
            }

            }

        /// <summary>
        /// Fetches all Doclink properties from the database and populates a DataTable.
        /// This method interacts directly with the database using a TableAdapter.
        /// </summary>
        /// <returns>A DataTable containing all Doclink properties.</returns>
        protected internal CommonDataSet.GetDoclinkPropertysDataTable GetDocumentPropertys()
        {
            Logger.Info("Step 10.0: Executing GetDocumentPropertys from database.");
            CommonDataSet.GetDoclinkPropertysDataTable dt = new CommonDataSet.GetDoclinkPropertysDataTable();
            try
            {
                using (GetDoclinkPropertysTableAdapter ta = new GetDoclinkPropertysTableAdapter())
                {
                    ta.Fill(dt);
                    Logger.Info("Step 10.1: Successfully filled Doclink properties table with {Count} rows.", dt.Rows.Count);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Step Error: Failed to retrieve Doclink properties from database.");
                throw;
            }
            return dt;
        }

        /// <summary>
        /// Retrieves the SIRM document type information for a specific document type name from the cache.
        /// </summary>
        /// <param name="documentType">The name of the document type to search for.</param>
        /// <returns>The DataRow containing the document type info if found; otherwise, null.</returns>
        protected internal CommonDataSet.ListSirmDocumentTypeInfoRow GetSirmDocumentTypeInfo(string documentType)
        {
            Logger.Info("Step 11.0: Fetching SIRM document type info for: '{DocumentType}'", documentType);
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
                    Logger.Info("Step 11.1: Document type '{DocumentType}' found in cache.", documentType);
                }
                else
                {
                    Logger.Warn("Step 11.2: Document type '{DocumentType}' NOT found in cache.", documentType);
                }

            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Step Error: Error retrieving document type info from cache for: '{DocumentType}'", documentType);
                throw;
            }
            return ret;
        }

        /// <summary>
        /// Retrieves the collator path for a given location code from the cache.
        /// This is used to route unrecognized documents to specific folders.
        /// </summary>
        /// <param name="location">The location code string.</param>
        /// <returns>The collator path string if found; otherwise, an empty string.</returns>
        protected internal string GetCollatorPath(string location)
        {
            Logger.Debug("Step 12.0: Fetching CollatorPath for Location: {Location}", location);
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
                        Logger.Info("Step 12.1: Found CollatorPath '{Path}' for location '{Location}'", ret, location);
                    }
                    else
                    {
                        Logger.Warn("Step 12.2: No CollatorPath found in cache for location '{Location}'", location);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Step Error: Error retrieving CollatorPath from cache for Location {Location}", location);
                throw;
            }
            return ret;
        }

        /// <summary>
        /// Fetches all active location collator paths from the database.
        /// This method interacts directly with the database using a TableAdapter.
        /// </summary>
        /// <returns>A DataTable containing location and collator path information.</returns>
        protected internal CommonDataSet.GetLocationCollatorPathsDataTable GetLocationCollatorPaths()
        {
            Logger.Info("Step 13.0: Executing GetLocationCollatorPaths from database.");
            CommonDataSet.GetLocationCollatorPathsDataTable dt = new CommonDataSet.GetLocationCollatorPathsDataTable();
            try
            {
                using (GetLocationCollatorPathsTableAdapter ta = new GetLocationCollatorPathsTableAdapter())
                {
                    ta.Fill(dt);
                }
                Logger.Info("Step 13.1: Successfully fetched {Count} collator paths from database.", dt.Rows.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Step Error: Failed to fetch Collator Paths from database.");
                throw;
            }

            return dt;

        }

        /// <summary>
        /// Fetches all primary and active SIRM document type definitions from the database.
        /// This method interacts directly with the database using a TableAdapter.
        /// </summary>
        /// <returns>A DataTable containing SIRM document type metadata and workflow settings.</returns>
        protected internal CommonDataSet.ListSirmDocumentTypeInfoDataTable ListSirmDocumentTypeInfo()
        {
            Logger.Info("Step 14.0: Executing ListSirmDocumentTypeInfo from database.");
            CommonDataSet.ListSirmDocumentTypeInfoDataTable dt = new CommonDataSet.ListSirmDocumentTypeInfoDataTable();
            try
            {
                using (ListSirmDocumentTypeInfoTableAdapter ta = new ListSirmDocumentTypeInfoTableAdapter())
                {
                    ta.Fill(dt);
                    Logger.Info("Step 14.1: Successfully fetched SIRM document type info table with {Count} rows.", dt.Rows.Count);
                }

            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Step Error: Failed to fetch SIRM document type info from database.");
                throw;

            }

            return dt;

        }

        /// <summary>
        /// Logs the final destination of a processed file into the database for auditing purposes.
        /// It uses a stored procedure to either update an existing record or insert a new one.
        /// </summary>
        /// <param name="fileName">The original filename of the processed document.</param>
        /// <param name="destination">The destination where the file was moved or indexed.</param>
        /// <returns>True if the destination was successfully logged; otherwise, false.</returns>
        internal static Boolean SetFileDestination(string fileName, string destination)
        {
            Logger.Info("Step 15.0: Logging file destination. File: {FileName}, Destination: {Destination}", fileName, destination);
            int ret = 1;

            try
            {
                using (QueriesTableAdapter ta = new QueriesTableAdapter())
                {
                    ret = ta.InsertImageIoMessageDestination(fileName, destination);
                    Logger.Info("Step 15.1: InsertImageIoMessageDestination executed. Result code: {Result}", ret);
                }

            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Step Error: Failed to set file destination in database. File: {FileName}, Destination: {Destination}", fileName, destination);
                throw;
            }
            return (ret == 0);

        }

    }
}
