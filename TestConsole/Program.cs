using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Maxum.EDM;

namespace TestConsole
{
   static class Program
   {
      
      static void Main(string[] args)
      {

            //GetLocationCollatorPathsTest();
            //ListDocumentTypesTest();

            ImageProcessing ip = new ImageProcessing();
            ip.StartProcessing();
            

            InsertImageIodestinationTest();
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
