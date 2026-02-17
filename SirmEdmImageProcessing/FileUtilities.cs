using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using NLog;
namespace Maxum.EDM
{
   internal static class FileUtilities
   {
      private static Properties.Settings _mySetings = Properties.Settings.Default;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


        internal static void SetupUnknownFolders(ref ProcessCache cache)
      {
            Logger.Info($"Starting SetupUnknownFolders for directory: {cache.UnknownDirectory ?? "null"}");
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
                     Logger.Info($"Found existing unknown working folder: {cache.UnknownWorkingFolder} with {cache.UnknownFolderFileCount} files.");
                  }
                  else
                  {
                     Logger.Info($"No suitable existing unknown working folder found for today's date: {todaysFolder}. A new one will be created.");
                  }
               }
            }
            else
            {
                 Logger.Info($"UnknownDirectoryInfo is null for: {cache.UnknownDirectory ?? "null"}. A new one will be created.");
            }
            Logger.Info("Finished SetupUnknownFolders.");
         }
         catch (Exception ex)
         {
            Logger.Error(ex, "Failed to set up unknown folders for directory: " + cache.UnknownDirectory);
            throw;
         }
      }
      
      internal static bool WriteValidationXML(ref ProcessCache cache)
      {
         Logger.Info($"Starting WriteValidationXML for file: {cache.WorkingFilePath}");
         bool ret = false;
         try
         {
            // Write the file then the xml data
            if (!Directory.Exists(cache.ValidationArchiveDirectory))
            {
               Logger.Info($"Validation archive directory does not exist. Attempting to create: {cache.ValidationArchiveDirectory}");
                    try
                    {
                        Directory.CreateDirectory(cache.ValidationArchiveDirectory);
                        Logger.Info($"Successfully created validation archive directory: {cache.ValidationArchiveDirectory}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to Create Directory on " + cache.ValidationArchiveDirectory);
                        throw; // Re-throw if directory creation is critical
                    }
            }
            if (File.Exists(cache.WorkingFilePath))
            {
                string destinationPath = Path.Combine(cache.ValidationArchiveDirectory, cache.WorkingFile);
                Logger.Info($"Attempting to copy file from {cache.WorkingFilePath} to {destinationPath}");
                    try
                    {
                        File.Copy(cache.WorkingFilePath, destinationPath, true);
                        Logger.Info($"Successfully copied file from {cache.WorkingFilePath} to {destinationPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Failed to Copy " + cache.WorkingFile + " From " + cache.WorkingFilePath + " To " +  cache.ValidationArchiveDirectory);
                        throw; // Re-throw if file copy is critical
                    }
            }
            else
            {
                Logger.Warn($"Working file does not exist, cannot copy: {cache.WorkingFilePath}");
                throw new FileNotFoundException($"Working file not found: {cache.WorkingFilePath}");
            }
            

            XElement validation =
               new XElement("FileAttributes",
                  new XElement("InsertTime", cache.ValidationInsertDateTime),
                  new XElement("DocumentID", cache.ValidationDocumentID.ToString()),
                  new XElement("CompletedDate", cache.ValidationCompleteDateTime)
                  );

            string savePath = Path.Combine(cache.ValidationArchiveDirectory, Path.GetFileNameWithoutExtension( cache.WorkingFile) + ".xml");
            Logger.Info($"Attempting to save XML validation file to: {savePath}");
                try
                {
                    validation.Save(savePath);
                    Logger.Info($"Successfully saved XML validation file to: {savePath}");
                    ret = true;

                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to save XML validation file to: " +  savePath);
                    throw;
                }
            Logger.Info($"Finished WriteValidationXML for file: {cache.WorkingFilePath}. Result: {ret}");
         }
         catch (Exception ex)
         {
                Logger.Error(ex, "Overall failure in WriteValidationXML for: " + cache.WorkingFilePath);
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
                Logger.Error(e, "Error cleaning directory name: " + directory);
                directory.TrimEnd(Path.DirectorySeparatorChar);
                //throw new IndexOutOfRangeException("Position: " + pos.ToString() + "   DirName: " + dirName + @"\n" + e.ToString());
        }
        return dirName;
     }


   }
}
