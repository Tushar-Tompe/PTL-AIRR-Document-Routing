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
        /// <summary>
        /// Main entry point for the Maxum.EDM Console application.
        /// This method initializes the image processing logic and orchestrates the overall execution flow.
        /// It includes NLog logging for tracking progress and error handling for any unhandled exceptions.
        /// </summary>
        static public void Main()
      {
            Logger.Info("Step 1: Maxum.EDM Console Processing Started.");
            try
            {
                Logger.Info("Step 2: Initializing ImageProcessing instance.");
                ImageProcessing ip = new ImageProcessing();
                Logger.Info("Step 3: Invoking ImageProcessing.StartProcessing method.");
                ip.StartProcessing();
                Logger.Info("Step 4: ImageProcessing completed successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Step Error: An unhandled exception occurred in the main application loop.");
            }
            finally
            {
                Logger.Info("Step 5: Application shutting down.");
            }
        }
   }
}
