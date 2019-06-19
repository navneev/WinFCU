using System.Diagnostics;
using System.ServiceProcess;
using Total.Util;

namespace Total.WinFCU
{
    public partial class WinFCUService : ServiceBase
    {

        public WinFCUService()
        {
            InitializeComponent();
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.CanShutdown = true;
            this.CanHandlePowerEvent = true;
            fcu.evtLog = new EventLog();
            if (!EventLog.SourceExists(ProjectInstaller.ServiceName)) { EventLog.CreateEventSource(ProjectInstaller.ServiceName, fcu.EventLogName); }
            fcu.evtLog.Source = ProjectInstaller.ServiceName;
            fcu.evtLog.Log = fcu.EventLogName;
            fcu.evtLog.WriteEntry("Initializing the WinFCU service");
            fcu.EventLogInitialized = true;
        }

        // --------------------------------------------------------------------------------------------------------------------
        //   Setup the service control methods (stop, start, pause, continue)
        // --------------------------------------------------------------------------------------------------------------------
        protected override void OnStart(string[] args) { runService(); }
        internal void runService() { WinFCU.runService(); }

        // --------------------------------------------------------------------------------------------------------------------
        protected override void OnPause() { pauseService(); }
        internal void pauseService()
        {
            writeLogMessage("The WinFCU Service service is paused...");
            suspendWinFCUSercvice();
        }

        // --------------------------------------------------------------------------------------------------------------------
        protected override void OnContinue() { continueService(); }
        internal void continueService()
        {
            writeLogMessage("The WinFCU Service service continues...");
            resumeWinFCUSercvice();
        }

        // --------------------------------------------------------------------------------------------------------------------
        protected override void OnStop() { stopService(); }
        internal void stopService()
        {
            writeLogMessage("The WinFCU Service service is stopping...");
            suspendWinFCUSercvice();
        }

        // --------------------------------------------------------------------------------------------------------------------
        protected override void OnShutdown() { shutdownService(); }
        internal void shutdownService()
        {
            stopService();
            base.OnShutdown();
        }

        // --------------------------------------------------------------------------------------------------------------------
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            if (powerStatus == PowerBroadcastStatus.Suspend)
            {
                writeLogMessage("The WinFCU Service service is suspended...");
                suspendWinFCUSercvice();
            }
            if (powerStatus == PowerBroadcastStatus.ResumeSuspend)
            {
                writeLogMessage("The WinFCU Service service is resuming...");
                resumeWinFCUSercvice();
            }
            return (powerStatus == PowerBroadcastStatus.PowerStatusChange);
        }

        // --------------------------------------------------------------------------------------------------------------------
        private void writeLogMessage(string Message)
        {
            fcu.evtLog.WriteEntry(Message);
            total.Logger.Info(Message);
        }

        // --------------------------------------------------------------------------------------------------------------------
        private void suspendWinFCUSercvice()
        {
            fcu.fcuTimer.Enabled = false;
            WinFCU.SetFileSystemWatchers(false);
        }

        // --------------------------------------------------------------------------------------------------------------------
        private void resumeWinFCUSercvice()
        {
            EventSchedule.nextRun nxtRun = fcu.evtSch.GetNextRun();
            fcu.fcuTimer.Interval = nxtRun.RelTime.TotalMilliseconds;
            fcu.fcuTimer.Enabled = true;
            WinFCU.SetFileSystemWatchers(true);
            WinFCU.ShowNextRunDetails(nxtRun);
        }

    }
}
