using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Maxum.EDM;
using NLog;

namespace TestConsole
{
   static class Program
   {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Main entry point for the TestConsole application.
        /// This method is primarily used for developer testing and debugging of the core image processing logic.
        /// It includes a test email trigger, initializes ImageProcessing, and invokes its main execution method.
        /// All operations are wrapped in NLog for detailed tracing and error reporting.
        /// </summary>
        /// <param name="args">Command-line arguments (not used).</param>
        static void Main(string[] args)
      {
            Logger.Info("Step 1: TestConsole application started.");
            Logger.Error("Step 2: TEST EMAIL - Program.cs: This is a direct test email attempt from Program.cs before any other app logic. SQL Error might follow."); // TEST EMAIL ALERT
            LogManager.Flush(); // Step 3: Force NLog to process logs immediately

            //GetLocationCollatorPathsTest();
            //ListDocumentTypesTest();
            try
            {
                Logger.Info("Step 4: Entering into main processing block.");
                Logger.Info("Step 5: Initializing ImageProcessing instance.");
                ImageProcessing ip = new ImageProcessing();
                Logger.Info("Step 6: Invoking ImageProcessing.StartProcessing.");
                ip.StartProcessing();
                Logger.Info("Step 7: ImageProcessing completed successfully.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Step Error: An unhandled exception occurred during main processing.");
                LogManager.Flush(); // Flush on error too
            }


            //InsertImageIodestinationTest();
            //SetupUnknownFoldersTest();


        }

        // ... existing commented out methods ...

        /// <summary>
        /// Test method for inserting image destination audit records into the database.
        /// This is used to verify the audit logging stored procedure and TableAdapter functionality.
        /// </summary>
        static void InsertImageIodestinationTest()
        {

            CommonData cdata = new CommonData();
            Maxum.EDM.CommonDataSetTableAdapters.QueriesTableAdapter ta = new Maxum.EDM.CommonDataSetTableAdapters.QueriesTableAdapter();
            ta.InsertImageIoMessageDestination(@"\\Test\Test.tif", "billsTest");
            ta.Dispose();
        }

        //static void SetupUnknownFoldersTest()
        //{
        //    Maxum.EDM.ProcessCache pc = new Maxum.EDM.ProcessCache();
        //    Maxum.EDM.FileUtilities.SetupUnknownFolders(ref pc);

        //}

    }
}
