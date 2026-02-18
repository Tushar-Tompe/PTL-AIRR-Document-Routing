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

        static void Main(string[] args)
      {
            Logger.Error("TEST EMAIL - Program.cs: This is a direct test email attempt from Program.cs before any other app logic. SQL Error might follow."); // TEST EMAIL ALERT
            LogManager.Flush(); // Force NLog to process logs immediately

            //GetLocationCollatorPathsTest();
            //ListDocumentTypesTest();
            try
            {
                Logger.Info("Entering into main method");
                Logger.Info("Intialized ImageProcessing");
                ImageProcessing ip = new ImageProcessing();
                Logger.Info("Invoked StartProcessing");
                ip.StartProcessing();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Received Error: ");
                LogManager.Flush(); // Flush on error too
            }


            //InsertImageIodestinationTest();
            //SetupUnknownFoldersTest();


        }

        //static void GetLocationCollatorPathsTest()
        //{
        //    CommonData cdata = new CommonData();

        //    Maxum.EDM.CommonDataSet.GetLocationCollatorPathsDataTable dt = cdata.GetLocationCollatorPaths();
        //    foreach (CommonDataSet.GetLocationCollatorPathsRow r in dt)
        //    {
        //        Console.WriteLine(r.Location + " - " + r.CollatorPath);

        //    }
        //    Console.ReadLine();
        //    dt.Dispose();
        //}

        //static void ListDocumentTypesTest()
        //{
        //    CommonData cdata = new CommonData();
        //    Maxum.EDM.CommonDataSet.ListDocumentTypesDataTable dt = cdata.ListDocumentTypes();
        //    foreach (CommonDataSet.ListDocumentTypesRow r in dt)
        //    {
        //        Console.WriteLine(r.DocumentTypeTag + " - " + r.KeyPropertyName);

        //    }
        //    Console.ReadLine();
        //    dt.Dispose();
        //}

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
