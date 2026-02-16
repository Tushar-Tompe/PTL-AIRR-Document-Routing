using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
namespace Maxum.EDM
{
   internal static class FileUtilities
   {
      private static Properties.Settings _mySetings = Properties.Settings.Default;

      internal static void SetupUnknownFolders(ref ProcessCache cache)
      {
         string todaysFolder = DateTime.Now.ToString("yyyy-MM-dd");
         DirectoryInfo[] UnknownDirectoryInfo = null;

         try
         {
            // Initialize the vars to the create new if no existing found below.
            cache.UnknownWorkingFolder = GetDateTime();
            cache.UnknownFolderFileCount = 0;

            if (cache.UnknownDirectory != null)
            {
               UnknownDirectoryInfo = new DirectoryInfo(CleanDirectoryName(cache.UnknownDirectory)).GetDirectories();
            }

            if (UnknownDirectoryInfo != null)
            {
               var d = from di in UnknownDirectoryInfo
                       where di.Name.Length > 10
                       && di.Name.Substring(0, 10) == todaysFolder
                       && di.GetFiles().Length < _mySetings.MaxUnknownFiles
                       orderby di.LastWriteTime descending
                       select di;

               if (d != null)
               {
                  List<DirectoryInfo> names = d.ToList<DirectoryInfo>();

                  if (names.Count() > 0)
                  {
                     cache.UnknownWorkingFolder = names[0].Name;
                     cache.UnknownFolderFileCount = names[0].GetFiles().Length;
                  }
               }
            }
         }
         catch (Exception)
         {

            throw;
         }
      }
      
      internal static bool WriteValidationXML(ref ProcessCache cache)
      {
         bool ret;
         try
         {
            // Write the file then the xml data
            if (!Directory.Exists(cache.ValidationArchiveDirectory))
            {
               Directory.CreateDirectory(cache.ValidationArchiveDirectory);
            }
            if (File.Exists(cache.WorkingFilePath))
            {
               File.Copy(cache.WorkingFilePath, Path.Combine(cache.ValidationArchiveDirectory, cache.WorkingFile),true);
            }
            

            XElement validation =
               new XElement("FileAttributes",
                  new XElement("InsertTime", cache.ValidationInsertDateTime),
                  new XElement("DocumentID", cache.ValidationDocumentID.ToString()),
                  new XElement("CompletedDate", cache.ValidationCompleteDateTime)
                  );

            string savePath = Path.Combine(cache.ValidationArchiveDirectory, Path.GetFileNameWithoutExtension( cache.WorkingFile) + ".xml");
            validation.Save(savePath);
            ret = true;
            
         }
         catch (Exception)
         {
            throw;
         }
         finally
         {
            cache.ValidationDocumentID = -1;
            cache.ValidationCompleteDateTime = string.Empty;
         }
         return ret;
      }

      //internal static void EDM_CreateValidationFile(string fullpath)
      //{
      //    using (FileStream writer = new FileStream(fullpath, FileMode.Create, FileAccess.Write))
      //    {
      //        EDM_ValidationMessage msg = new EDM_ValidationMessage { CompletedDate = DateTime.Now, DocumentID = 2, InsertTime = DateTime.Now };
      //        DataContractSerializer dcs = new DataContractSerializer(typeof(EDM_ValidationMessage));
      //        dcs.WriteObject(writer, msg);
      //    }
      //}
     internal static string GetDateTime()
      {
         return DateTime.Now.ToString("yyyy-MM-dd Hmmss");
      }

     internal static string CleanDirectoryName(string directory)
     {
        string dirName = directory;
        int pos = 0;
        try
        {
           // Directory won't resolve correctly if it has a \ on the end.
          
           if (directory.Length > 0)
           {
              pos = dirName.LastIndexOf(@"\");
              if (pos == dirName.Length - 1)
              {
                 dirName = dirName.Remove(pos);
              }
           }
         
        }
        catch (Exception e)
        {

           throw new IndexOutOfRangeException("Position: " + pos.ToString() + "   DirName: " + dirName + @"\n" + e.ToString());
        }
        return dirName;
     }


   }
}
