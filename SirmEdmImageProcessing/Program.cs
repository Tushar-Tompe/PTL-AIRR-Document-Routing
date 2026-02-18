using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
namespace Maxum.EDM
{
   static class Program
   {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        static public void Main()
      {
            Logger.Info("=== Maxum.EDM Console Processing Started ===");
            try
            {
                ImageProcessing ip = new ImageProcessing();
                Logger.Info("ImageProcessing initialized.");
                ip.StartProcessing();
                Logger.Info("Processing completed successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                Logger.Info("Application shutting down.");
            }

        }
   }
}
