using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using NLog; // Added for NLog

namespace Maxum.EDM
{
    /// <summary>
    /// Implements the Sirm Document Routing Windows Service. This service monitors a configured folder
    /// for image files, processes them using the ImageProcessing component, and integrates with Doclink.
    /// It uses a timer to periodically trigger the image processing workflow.
    /// </summary>
    public partial class SirmDocumentRoutingService : ServiceBase
   {
      private static readonly Logger Logger = LogManager.GetCurrentClassLogger(); // Added for NLog
      private Properties.Settings _mySetings = Properties.Settings.Default;
      private  Timer _timer;
      private Maxum.EDM.ImageProcessing _ip;
        /// <summary>
        /// Initializes a new instance of the <see cref="SirmDocumentRoutingService"/> class.
        /// Configures the EventLog source for service-related logging.
        /// </summary>
        public SirmDocumentRoutingService()
      {
         InitializeComponent();
         Logger.Info("Step 20.0: Initializing SirmDocumentRoutingService.");
         if (!System.Diagnostics.EventLog.SourceExists("SIRM Document Routing"))
         {
            System.Diagnostics.EventLog.CreateEventSource("SirmDocRouter", "SIRM Document Routing", ".");
         }

         eventLog1.Source = "SirmDocRouter";
         eventLog1.Log = "SIRM Document Routing";
      }

        /// <summary>
        /// Called when the service starts.
        /// It initializes and enables a timer that periodically triggers the image processing logic.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the service.</param>
        protected override void OnStart(string[] args)
      {
         Logger.Info("Step 21.0: SirmDocumentRoutingService.OnStart initiated.");
         try
         {
            Logger.Info("Step 21.1: Setting up timer with interval: {Interval} minutes.", _mySetings.TimerIntervalInMinutes);
            eventLog1.WriteEntry("Starting Service");
            double interval = _mySetings.TimerIntervalInMinutes * 60000;
            _timer = new Timer(interval);
            _timer.Enabled = true;
            _timer.Elapsed += new ElapsedEventHandler(_timer_Elapsed);
            Logger.Info("Step 21.2: Timer enabled and service started successfully.");
         }
         catch (Exception ex)
         {
            Logger.Fatal(ex, "Step Fatal Error: Failed to start service or initialize timer.");
            eventLog1.WriteEntry(ex.ToString(), EventLogEntryType.Error, 100);
         }

         
      }

        /// <summary>
        /// Handles the Elapsed event of the timer.
        /// This method disables the timer, executes the image processing workflow,
        /// and then re-enables the timer for the next interval.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">An <see cref="ElapsedEventArgs"/> object that contains the event data.</param>
        void _timer_Elapsed(object sender, ElapsedEventArgs e)
      {
         Logger.Info("Step 22.0: Timer elapsed event fired. Disabling timer for processing.");
         _timer.Enabled = false;
         try
         {
            Logger.Info("Step 22.1: Creating new ImageProcessing instance and starting core logic.");
            _ip = new ImageProcessing();
            _ip.StartProcessing();
            Logger.Info("Step 22.2: Image processing cycle completed successfully.");
         }
         catch (Exception ex)
         {
            Logger.Error(ex, "Step 22.3 Error: An exception occurred during the timed processing cycle.");
            eventLog1.WriteEntry("Processing error in Timer \n" + ex.ToString());
         }
         finally
         {
               Logger.Info("Step 22.4: Re-enabling timer for next interval.");
               _timer.Enabled = true;   
         }
      }

        /// <summary>
        /// Called when the service stops.
        /// It disables the timer and unsubscribes from its event to ensure a clean shutdown.
        /// </summary>
        protected override void OnStop()
      {
         Logger.Info("Step 23.0: SirmDocumentRoutingService.OnStop initiated.");
         eventLog1.WriteEntry("Stopping Service");
         _timer.Enabled = false;
         _timer.Elapsed -= new ElapsedEventHandler(_timer_Elapsed);
         Logger.Info("Step 23.1: Timer disabled and service stopped successfully.");
      }
   }
}
