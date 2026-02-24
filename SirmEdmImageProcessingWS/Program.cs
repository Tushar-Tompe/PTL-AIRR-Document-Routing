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
        /// <summary>
        /// Main entry point for the SirmDocumentRoutingService application.
        /// This method initializes and starts the Windows Service responsible for background document routing.
        /// It handles fatal exceptions during service startup and provides NLog status updates.
        /// </summary>
        static void Main()
        {
            Logger.Info("Step 1: SirmDocumentRoutingService application main entry point started.");
            try
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                   {
                new SirmDocumentRoutingService()

                   };
                Logger.Info("Step 2: Starting SirmDocumentRoutingService(s).");
                ServiceBase.Run(ServicesToRun);
                Logger.Info("Step 3: SirmDocumentRoutingService(s) stopped gracefully.");
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Step Error: An unhandled fatal exception occurred in the service main method.");
                throw;
            }

            }
   }
}
