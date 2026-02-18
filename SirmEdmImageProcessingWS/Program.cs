using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using NLog;


namespace Maxum.EDM
{
   static class Program
   {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        static void Main()
      {
            Logger.Info("Application starting...");
            try
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                   {
                new SirmDocumentRoutingService()

                   };
                ServiceBase.Run(ServicesToRun);
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Unhandled exception in Main(): " + ex.Message);
                throw;
            }

            }
   }
}
