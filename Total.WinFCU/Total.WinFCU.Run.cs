using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Total.CLI;
using Total.Util;
using log4net.Core;

namespace Total.WinFCU
{
    public partial class WinFCU
    {
        // ====================================================================================================================
        //  WinFCU - Windows Filesystem Cleanup Utility
        //
        //   The purpose of this utility is to prevent disks from filling up with unwanted/unneeded files. To do so it uses
        //   an XML configuration file which contains directives to determine which files to erase, move, compact or archive.
        //   
        //
        //                                                                      January 2015
        //                                                                      Hans van Veen, Total Productions
        //
        //
        //   Interactive/Batch Commandline options:
        //     -logfile         Send log messages to the specified file
        //     -export          Export the resulting configuration to a file
        //     -[no]debug       Show debug information when running (overrules the setting in the config file)
        //     -[no]dryrun      Show what would have happend (overrules the setting in the config file)
        //     -schedule        Run WinFCU but only for the specified schedule
        //     -service         Install or unistall WinFCU as service
        //     -show            Show WinFCU info
        //                      -show license         Show WinFCU license info
        //                      -show version         Show WinFCU version info
        //                      -show status          Show WinFCU status info
        //                      -show schedule        Show WinFCU schedule info
        //                      -show service         Show WinFCUService info
        //     -help, -?        Show the help information (this info plus a few additional things)
        // ====================================================================================================================
        public static EventSchedule.nextRun nxtRun = new EventSchedule.nextRun();
        private static List<FileSystemWatcher> fswList = new List<FileSystemWatcher>();

        public static void runInteractive(string[] args)
        {
            // ====================================================================================================================
            //   Start with initializing and loading the commandline options so they can be used during further initialization
            // --------------------------------------------------------------------------------------------------------------------
            cli.SetParameterIndicator("-");
            cli.SetSecretMarker('*');
            cli.AddDefinition("{negatable,notnullorempty}[string]logfile");
            cli.AddDefinition("[string]export=.\\WinFCU_Export.config");
            cli.AddDefinition("{negatable,alias(verbose)}[bool]debug");
            cli.AddDefinition("{negatable,alias(whatif)}[bool]dryrun");
            cli.AddDefinition("{mandatory,notnullorempty}[string[]]schedule=#ALL#");
            cli.AddDefinition("{notnullorempty,validateset(keywords|license|version|status|schedule|service)}[string]show");
            cli.AddDefinition("{notnullorempty,validateset(install|uninstall|start|stop|restart|status)}[string]service");
            cli.AddDefinition("{alias(?)}[bool]help");
            cli.AddRule("<disallow any2(export,show,help,logfile,service)>");
            cli.LoadDefinitions();
            // --------------------------------------------------------------------------------------------------------------------
            //   CLI Definitions are loaded. Some options do not require the full stack to be loaded. Lets deal with them now
            // --------------------------------------------------------------------------------------------------------------------
            if (cli.IsPresent("Help")) { fcu.ShowHelp(); Environment.Exit(0); }
            // --------------------------------------------------------------------------------------------------------------------
            //  Initialize the application and logging context
            // --------------------------------------------------------------------------------------------------------------------
            string logFilename = null;
            if (cli.IsPresent("logfile")) { logFilename = fcu.ReplaceKeyword(cli.GetValue("logfile")); }
            Level logLevel = (cli.IsPresent("Debug") && !cli.IsNegated("Debug")) ? Level.Debug : Level.Info;
            if (!(cli.IsNegated("logfile") || cli.IsPresent("Export"))) { total.InitializeLog4Net(logFilename, logLevel); }
            // --------------------------------------------------------------------------------------------------------------------
            //   fcu.LoadConfiguration         Load the WinFCU configuration from the provided input file
            // --------------------------------------------------------------------------------------------------------------------
            fcu.LoadConfiguration(total.APP.Config);
            // --------------------------------------------------------------------------------------------------------------------
            //   Configuration has been loaded, if show is requested we can do so now and exit
            // --------------------------------------------------------------------------------------------------------------------
            if (cli.IsPresent("Show"))
            {
                switch (cli.GetValue("Show").ToLower())
                {
                    case "keywords": total.ShowReplacementKeywords("WinFCU"); break;
                    case "license": fcu.ShowLicense(); break;
                    case "version": fcu.ShowVersion(); break;
                    case "status": fcu.ShowStatus(); break;
                    case "schedule": fcu.ShowSchedule(); break;
                    case "service": fcu.ShowService(); break;
                    default: fcu.ShowHelp(); break;
                }
                Environment.Exit(0);
            }
            // --------------------------------------------------------------------------------------------------------------------
            //  Install/Uninstall WinFCU as service ??
            // --------------------------------------------------------------------------------------------------------------------
            if (cli.IsPresent("Service"))
            {
                bool svcInstalled;
                ServiceController sc = null;
                try { sc = new ServiceController(ProjectInstaller.SvcServiceName); svcInstalled = sc.ServiceName == ProjectInstaller.SvcServiceName; }
                catch (Exception) { svcInstalled = false; }

                string svcRequest = cli.GetValue("Service").ToLower();

                try
                {
                    switch (svcRequest)
                    {
                        case "install":
                            if (!svcInstalled) {
                                ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                                total.Logger.Info("The WinFCU service has been installed");
                            }
                            else
                            {
                                Console.WriteLine("The WinFCU service is already installed");
                            }
                            break;
                        case "uninstall":
                            if (svcInstalled)
                            {
                                ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
                                total.Logger.Info("The WinFCU service has been uninstalled");
                            }
                            else
                            {
                                Console.WriteLine("The WinFCU service is not installed");
                            }
                            break;
                        case "start":
                            if (svcInstalled) {
                                if (sc.Status != ServiceControllerStatus.Running)
                                {
                                    sc.Start();
                                    sc.WaitForStatus(ServiceControllerStatus.Running);
                                }
                                else
                                {
                                    Console.WriteLine("The WinFCU service is already running");
                                }
                            }
                            break;
                        case "stop":
                            if (svcInstalled) {
                                if (sc.Status != ServiceControllerStatus.Stopped)
                                {
                                    sc.Stop();
                                    sc.WaitForStatus(ServiceControllerStatus.Stopped);
                                }
                                else
                                {
                                    Console.WriteLine("The WinFCU service is not running");
                                }
                            }
                            break;
                        case "restart":
                            if (svcInstalled)
                            {
                                total.Logger.Info("The WinFCU service is restarting.......");
                                if (sc.Status != ServiceControllerStatus.Stopped) { sc.Stop(); }
                                sc.WaitForStatus(ServiceControllerStatus.Stopped);
                                sc.Start();
                                sc.WaitForStatus(ServiceControllerStatus.Running);
                            }
                            break;
                        case "status":
                            if (!svcInstalled) {
                                Console.WriteLine("The WinFCU service is Not Installed");
                            }
                            else {
                                Console.WriteLine("The WinFCU service is " + sc.Status.ToString());
                            }
                            break;
                    }
                }
                finally { sc.Dispose();  Environment.Exit(0); }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Also if export is requested we can do so now and exit
            // --------------------------------------------------------------------------------------------------------------------
            if (cli.IsPresent("Export"))
            {
                string exportFile = fcu.ReplaceKeyword(cli.GetValue("Export"));
                total.Logger.Debug("Exporting active configuration to: " + exportFile);
                fcu.fcuConfig.Save(exportFile);
                total.Logger.Info("Exported active configuration to: " + exportFile);
                Environment.Exit(0);
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Run the cleanup for every requested schedule (do not forget the log4net schedule global property!)
            // --------------------------------------------------------------------------------------------------------------------
            string[] scheduleNames = fcu.ReplaceKeyword(cli.GetValue("Schedule")).Split(',');
            total.Logger.Debug("Running WinFCU for schedules: " + string.Join(", ", scheduleNames));
            // --------------------------------------------------------------------------------------------------------------------
            //   Each cleanup schedule can run as a separate task, so more than one cleanup can run simultaneously
            // --------------------------------------------------------------------------------------------------------------------
            fcu.ZeroTotalCounters();
            foreach (string scheduleName in scheduleNames)
            {
                if (fcu.parallelSchedules) { var t = Task.Run(() => fcu.CleanFileSystem(scheduleName)); }
                else { fcu.CleanFileSystem(scheduleName); }
            }
        }

        public static void runService()
        {
            // ================================================================================================================
            //   Initialize log4net and write a start message to both the eventlog and log4net
            // ----------------------------------------------------------------------------------------------------------------
            total.InitializeLog4Net();
            string startMessage = "The WinFCU service is starting... - Config: " + total.APP.Config;
            fcu.evtLog.WriteEntry(startMessage);
            total.Logger.Info(startMessage);
            // ----------------------------------------------------------------------------------------------------------------
            //   Load the config file
            // ----------------------------------------------------------------------------------------------------------------
            fcu.LoadConfiguration(total.APP.Config);
            fcu.evtLog.WriteEntry("The WinFCU service is started...\n" + fcu.GetStatus());
            // ----------------------------------------------------------------------------------------------------------------
            //  Enable FileSystemWatchers for all files in the config folder(s) and core config file(s)
            // ----------------------------------------------------------------------------------------------------------------
            SetFileSystemWatchers(true);
            // ----------------------------------------------------------------------------------------------------------------
            //   Now that the configs have been loaded etc we can fetch the next scheduled run time
            // ----------------------------------------------------------------------------------------------------------------
            nxtRun = fcu.evtSch.GetNextRun();
            ShowNextRunDetails(nxtRun);
            // ----------------------------------------------------------------------------------------------------------------
            //   Set a timer for the requested interval and enable it. Disable repeated events, the timer needs to be set
            //   every time it has elapsed
            // ----------------------------------------------------------------------------------------------------------------
            fcu.fcuTimer = new Timer(nxtRun.RelTime.TotalMilliseconds);
            fcu.fcuTimer.Elapsed += tmrEventHandler;
            fcu.fcuTimer.AutoReset = false;
            fcu.fcuTimer.Enabled = true;
            // ----------------------------------------------------------------------------------------------------------------
            //   Run the cleanup for every requested schedule (do not forget the log4net schedule global property!)
            // ----------------------------------------------------------------------------------------------------------------
        }

        private static void fswEventHandler(Object fswObject, FileSystemEventArgs fswEventArgs)
        {
            // ----------------------------------------------------------------------------------------------------------------
            //   Something happend with a config file (new file, modified, deleted,... you name it)
            //   Disable the FileSystemWatchers and Timer and report the event
            // ----------------------------------------------------------------------------------------------------------------
            SetFileSystemWatchers(false);
            fcu.fcuTimer.Enabled = false;
            string fswMessage = String.Format("FSW Event {0}: {1} {2}", DateTime.Now.Ticks, fswEventArgs.ChangeType, fswEventArgs.FullPath);
            total.Logger.Info(fswMessage);
            fcu.evtLog.WriteEntry(fswMessage);
            // ----------------------------------------------------------------------------------------------------------------
            //   Reload the configuration files and get the details for the next scheduled run
            // ----------------------------------------------------------------------------------------------------------------
            fcu.LoadConfiguration(total.APP.Config);
            nxtRun = fcu.evtSch.GetNextRun();
            ShowNextRunDetails(nxtRun);
            // ----------------------------------------------------------------------------------------------------------------
            //   When done re-enable the timer and FileSystemWatchers
            // ----------------------------------------------------------------------------------------------------------------
            SetFileSystemWatchers(true);
            fcu.fcuTimer.Interval = nxtRun.RelTime.TotalMilliseconds;
            fcu.fcuTimer.Enabled = true;
        }

        private static void tmrEventHandler(Object tmrObject, ElapsedEventArgs tmrEventArgs)
        {
            // ----------------------------------------------------------------------------------------------------------------
            //   A timer elapse event occurred, disable the Timer and all FileSystemWatcher before running the cleanup
            //   Each cleanup schedule can run as a separate task, so more than one cleanup can run simultaneously
            // ----------------------------------------------------------------------------------------------------------------
            Timer fcuTimer = (Timer)tmrObject;
            fcu.fcuTimer.Enabled = false;
            SetFileSystemWatchers(false);
            fcu.ZeroTotalCounters();
            foreach (string scheduleName in nxtRun.Schedule.Split(','))
            {
                if (fcu.parallelSchedules) { var t = Task.Run(() => fcu.CleanFileSystem(scheduleName)); }
                else { fcu.CleanFileSystem(scheduleName); }
            }
            // ----------------------------------------------------------------------------------------------------------------
            //   Get the details for the next scheduled run
            // ----------------------------------------------------------------------------------------------------------------
            nxtRun = fcu.evtSch.GetNextRun();
            ShowNextRunDetails(nxtRun);
            // ----------------------------------------------------------------------------------------------------------------
            //   When done re-enable the timer and FileSystemWatchers
            // ----------------------------------------------------------------------------------------------------------------
            SetFileSystemWatchers(true);
            fcu.fcuTimer.Interval = nxtRun.RelTime.TotalMilliseconds;
            fcu.fcuTimer.Enabled = true;
        }

        public static void ShowNextRunDetails(EventSchedule.nextRun nxtRun)
        {
            string schMsg = String.Format("Next run in {0} sec. at {1} (schedule: {2})", (Int32)nxtRun.RelTime.TotalSeconds, nxtRun.AbsTime.ToString(), nxtRun.Schedule);
            fcu.evtLog.WriteEntry(schMsg);
            total.Logger.Info(schMsg);
        }

        public static void SetFileSystemWatchers(bool fswStatus)
        {
            if (!fswStatus)
            {
                foreach (FileSystemWatcher fsw in fswList) { fsw.EnableRaisingEvents = false; }
                return;
            }
            fswList.Clear();
            foreach (string fswTarget in fcu.fswTargets)
            {
                total.Logger.Debug("Adding FSW for: " + fswTarget);
                FileSystemWatcher fsw = new FileSystemWatcher(Path.GetDirectoryName(fswTarget), Path.GetFileName(fswTarget));
                fsw.Changed += fswEventHandler;
                fsw.Created += fswEventHandler;
                fsw.Deleted += fswEventHandler;
                fsw.Renamed += fswEventHandler;
                fsw.EnableRaisingEvents = true;
                fswList.Add(fsw);
            }
        }

//        public static bool regexCase(string switchInput, string matchInput)
//        {
//            int i = matchInput.Length - 1;
//            string regexMatch = matchInput[i].ToString();
//            for (int j = i-1; j >= 0; j--) { regexMatch = matchInput[j].ToString() + '(' + regexMatch + @")?"; }
//            Regex rgx = new Regex('^' + regexMatch + '$');
//            return rgx.IsMatch(switchInput);
//        }
    }
}
