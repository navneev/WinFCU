using System;
using Total.Util;
using Total.CLI;
using log4net.Core;

namespace Total.WinFCU
{
    public class FCUMain
    {
        static void Main()
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
            //   Commandline options:
            //     -logfile         Send log messages to the specified file
            //     -export          Export the resulting configuration to a file
            //     -[no]debug       Show debug information when running (overrules the setting in the config file)
            //     -[no]dryrun      Show what would have happend (overrules the setting in the config file)
            //     -schedule        Run WinFCU but only for the specified schedule
            //     -show            Show WinFCU info
            //                      -show license         Show WinFCU license info
            //                      -show version         Show WinFCU version info
            //                      -show status          Show WinFCU status info
            //                      -show schedule        Show WinFCU schedule info
            //     -help, -?        Show the help information (this info plus a few additional things)
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
            cli.AddDefinition("{notnullorempty,validateset(license|version|status|schedule)}[string]show");
            cli.AddDefinition("{alias(?)}[bool]help");
            cli.AddRule("<disallow any2(export,show,help,logfile)>");
            cli.LoadDefinitions();
            // --------------------------------------------------------------------------------------------------------------------
            //  Initialize the application context (create/fill data structures)
            //  See whether the required application configuration has been provided
            // --------------------------------------------------------------------------------------------------------------------
            total.InitializeContext();
            if (!total.APP.HasConfigFile)
            {
                ConsoleColor fg = Console.ForegroundColor; ConsoleColor bg = Console.BackgroundColor;
                Console.ForegroundColor = ConsoleColor.Red; Console.BackgroundColor = ConsoleColor.Black;
                Console.WriteLine(@" >>>> WinFCU Fatal Error - No application configuration file found!");
                Console.ForegroundColor = fg; Console.BackgroundColor = bg;
                Environment.Exit(1);
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   CLI Definitions are loaded. Some options do not require the full stack to be loaded. Lets deal with them now
            // --------------------------------------------------------------------------------------------------------------------
            if (cli.IsPresent("Help")) { fcu.ShowHelp(); Environment.Exit(0); }
            // --------------------------------------------------------------------------------------------------------------------
            //  Initialize the application and logging context
            // --------------------------------------------------------------------------------------------------------------------
            string logFilename = null;
            if (cli.IsPresent("logfile")) { logFilename = total.ReplaceKeyword(cli.GetValue("logfile")); }
            Level logLevel = (cli.IsPresent("Debug") && !cli.IsNegated("Debug")) ? Level.Debug : Level.Info;
            total.InitializeLog4Net(logFilename, logLevel);
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
                    case "license":  fcu.ShowLicense(); break;
                    case "version":  fcu.ShowVersion(); break;
                    case "status":   fcu.ShowStatus(); break;
                    case "schedule": fcu.ShowSchedule(); break;
                    default: fcu.ShowHelp(); break;
                }
                Environment.Exit(0);
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Also if export is requested we can do so now and exit
            // --------------------------------------------------------------------------------------------------------------------
            if (cli.IsPresent("Export"))
            {
                string exportFile = total.ReplaceKeyword(cli.GetValue("Export"));
                total.Logger.Debug("Exporting active configuration to: " + exportFile);
                fcu.fcuConfig.Save(exportFile);
                total.Logger.Info("Exported active configuration to: " + exportFile);
                Environment.Exit(0);
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Run the cleanup for every requested schedule (do not forget the log4net schedule global property!)
            // --------------------------------------------------------------------------------------------------------------------
            string[] scheduleNames = total.ReplaceKeyword(cli.GetValue("Schedule")).Split(',');
            total.Logger.Debug("Running WinFCU for schedules: " + string.Join(", ", scheduleNames));
            foreach (string scheduleName in scheduleNames) { fcu.CleanFileSystem(scheduleName); }
        }
    }
}
