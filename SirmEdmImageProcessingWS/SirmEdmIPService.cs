using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Timers;

namespace Maxum.EDM
{
   public partial class SirmDocumentRoutingService : ServiceBase
   {
      private Properties.Settings _mySetings = Properties.Settings.Default;
      private  Timer _timer;
      private Maxum.EDM.ImageProcessing _ip;
      public SirmDocumentRoutingService()
      {
         InitializeComponent();
         if (!System.Diagnostics.EventLog.SourceExists("SIRM Document Routing"))
         {
            System.Diagnostics.EventLog.CreateEventSource("SirmDocRouter", "SIRM Document Routing", ".");
         }

         eventLog1.Source = "SirmDocRouter";
         eventLog1.Log = "SIRM Document Routing";
      }

      protected override void OnStart(string[] args)
      {
         try
         {
            eventLog1.WriteEntry("Starting Service");
            double interval = _mySetings.TimerIntervalInMinutes * 60000;
            _timer = new Timer(interval);
            _timer.Enabled = true;
            _timer.Elapsed += new ElapsedEventHandler(_timer_Elapsed);
         }
         catch (Exception ex)
         {
            eventLog1.WriteEntry(ex.ToString(), EventLogEntryType.Error, 100);
            
         }

         
      }

      void _timer_Elapsed(object sender, ElapsedEventArgs e)
      {
         _timer.Enabled = false;
         try
         {
            _ip = new ImageProcessing();
            _ip.StartProcessing();
         }
         catch (Exception ex)
         {
            eventLog1.WriteEntry("Processing error in Timer \n" + ex.ToString());
         }
         finally
         {
               _timer.Enabled = true;   
         }
      }

      protected override void OnStop()
      {
         eventLog1.WriteEntry("Stopping Service");
         _timer.Enabled = false;
         _timer.Elapsed -= new ElapsedEventHandler(_timer_Elapsed);
         
      }
   }
}
