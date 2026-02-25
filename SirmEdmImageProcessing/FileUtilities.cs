using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using NLog;
namespace Maxum.EDM
{
    /// <summary>
    /// Provides utility methods for file system operations, particularly concerning the management
    /// of unknown folders and the writing of validation XML files for processed documents.
    /// </summary>
    internal static class FileUtilities
   {
      private static Properties.Settings _mySetings = Properties.Settings.Default;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


        /// <summary>
        /// Sets up the appropriate unknown folder structure for a given ProcessCache.
        /// It determines if an existing folder for the current day can be used or creates a new one,
        /// ensuring that the number of files per folder does not exceed a configured maximum.
        /// </summary>
        /// <param name="cache">A reference to the ProcessCache containing relevant directory and file count information.</param>
        internal static void SetupUnknownFolders(ref ProcessCache cache)
      {
            Logger.Info($"Step 16.0: Starting SetupUnknownFolders for directory: {cache.UnknownDirectory ?? "null"}");
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
                     Logger.Info($"Step 16.1: Found existing unknown working folder: {cache.UnknownWorkingFolder} with {cache.UnknownFolderFileCount} files.");
                  }
                  else
                  {
                     Logger.Info($"Step 16.2: No suitable existing unknown working folder found for today's date: {todaysFolder}. A new one will be created.");
                  }
               }
            }
            else
            {
                 Logger.Info($"Step 16.3: UnknownDirectoryInfo is null for: {cache.UnknownDirectory ?? "null"}. A new one will be created.");
            }
            Logger.Info("Step 16.4: Finished SetupUnknownFolders.");
         }
         catch (Exception ex)
         {
            Logger.Error(ex, "Step 16.5 Error: Failed to set up unknown folders for directory: " + cache.UnknownDirectory);
            throw;
         }
      }
      
              /// <summary>
              /// Writes a validation XML file for a processed document.
              /// This includes copying the original file to a validation archive directory and
              /// creating an XML file with document attributes for auditing and validation purposes.
              /// </summary>
              /// <param name="cache">A reference to the ProcessCache containing document and validation data.</param>
              /// <returns>True if the validation XML and file copy were successful; otherwise, false.</returns>
              internal static bool WriteValidationXML(ref ProcessCache cache)      {
         Logger.Info($"Step 17.0: Starting WriteValidationXML for file: {cache.WorkingFilePath}");
         bool ret = false;
         try
         {
            // Write the file then the xml data
            if (!Directory.Exists(cache.ValidationArchiveDirectory))
            {
               Logger.Info($"Step 17.1: Validation archive directory does not exist. Attempting to create: {cache.ValidationArchiveDirectory}");
                    try
                    {
                        Directory.CreateDirectory(cache.ValidationArchiveDirectory);
                        Logger.Info($"Step 17.2: Successfully created validation archive directory: {cache.ValidationArchiveDirectory}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Step 17.3 Error: Failed to Create Directory on " + cache.ValidationArchiveDirectory);
                        throw; // Re-throw if directory creation is critical
                    }
            }
            if (File.Exists(cache.WorkingFilePath))
            {
                string destinationPath = Path.Combine(cache.ValidationArchiveDirectory, cache.WorkingFile);
                Logger.Info($"Step 17.4: Attempting to copy file from {cache.WorkingFilePath} to {destinationPath}");
                    try
                    {
                        File.Copy(cache.WorkingFilePath, destinationPath, true);
                        Logger.Info($"Step 17.5: Successfully copied file from {cache.WorkingFilePath} to {destinationPath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Step 17.6 Error: Failed to Copy " + cache.WorkingFile + " From " + cache.WorkingFilePath + " To " +  cache.ValidationArchiveDirectory);
                        throw; // Re-throw if file copy is critical
                    }
            }
            else
            {
                Logger.Warn($"Step 17.7 Warning: Working file does not exist, cannot copy: {cache.WorkingFilePath}");
                throw new FileNotFoundException($"Working file not found: {cache.WorkingFilePath}");
            }
            

            XElement validation =
               new XElement("FileAttributes",
                  new XElement("InsertTime", cache.ValidationInsertDateTime),
                  new XElement("DocumentID", cache.ValidationDocumentID.ToString()),
                  new XElement("CompletedDate", cache.ValidationCompleteDateTime)
                  );

            string savePath = Path.Combine(cache.ValidationArchiveDirectory, Path.GetFileNameWithoutExtension( cache.WorkingFile) + ".xml");
            Logger.Info($"Step 17.8: Attempting to save XML validation file to: {savePath}");
                try
                {
                    validation.Save(savePath);
                    Logger.Info($"Step 17.9: Successfully saved XML validation file to: {savePath}");
                    ret = true;

                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Step 17.10 Error: Failed to save XML validation file to: " +  savePath);
                    throw;
                }
            Logger.Info($"Step 17.11: Finished WriteValidationXML for file: {cache.WorkingFilePath}. Result: {ret}");
         }
         catch (Exception ex)
         {
                Logger.Error(ex, "Step 17.12 Error: Overall failure in WriteValidationXML for: " + cache.WorkingFilePath);
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
        /// <summary>
        /// Generates a formatted date and time string suitable for use in file or folder names.
        /// The format is "yyyy-MM-dd Hmmss".
        /// </summary>
        /// <returns>A string representing the current date and time in "yyyy-MM-dd Hmmss" format.</returns>
        internal static string GetDateTime()
      {
         return DateTime.Now.ToString("yyyy-MM-dd Hmmss");
      }

        /// <summary>
        /// Cleans a directory name by removing any trailing directory separator characters.
        /// This ensures that directory paths are consistently formatted.
        /// </summary>
        /// <param name="directory">The directory path string to clean.</param>
        /// <returns>The cleaned directory path string.</returns>
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
                Logger.Error(e, "Step 18.0 Error: cleaning directory name: " + directory);
                directory.TrimEnd(Path.DirectorySeparatorChar);
                //throw new IndexOutOfRangeException("Position: " + pos.ToString() + "   DirName: " + dirName + @"\n" + e.ToString());
        }
        return dirName;
     }


   }
}
