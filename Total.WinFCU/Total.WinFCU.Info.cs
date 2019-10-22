using System;
using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;
using Total.Util;

namespace Total.WinFCU
{
    // ====================================================================================================================
    //  FCUinfo - Show various items of WinFCU information, like version info and help info 
    // --------------------------------------------------------------------------------------------------------------------
    public partial class fcu
    {
        private static string fcuFilename = Process.GetCurrentProcess().MainModule.FileVersionInfo.FileName;
        private static string fcuPath     = System.IO.Path.GetDirectoryName(fcuFilename);
        public  static string fcuVersion  = FileVersionInfo.GetVersionInfo(fcuFilename).FileVersion;
        public  static int    curYear     = DateTime.Today.Year;

        // ====================================================================================================================
        //  WinFCU Show Servcice - Show the WinFCU service information (when installed as service!)
        // --------------------------------------------------------------------------------------------------------------------
        public static void ShowService()
        {
            try
            {
                // Get all possible service info
                ServiceController sc = new ServiceController(ProjectInstaller.SvcServiceName);
                string svcKey = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Services\\" + ProjectInstaller.SvcServiceName;
                string startType = "Automatic";
                int svc_StartType = (int)Registry.GetValue(svcKey, "Start", -1);
                if (svc_StartType == (int)ServiceStartMode.Manual) { startType = "Manual"; }
                if (svc_StartType == (int)ServiceStartMode.Disabled) { startType = "Disabled"; }
                if ((svc_StartType == (int)ServiceStartMode.Automatic) && ((int)Registry.GetValue(svcKey, "DelayedAutostart", 0) == 1)) { startType = "Automatic (Delayed Start)"; }
                // Show what we have got.....
                string svcInfo = String.Format("\r\n WinFCU Service Info\r\n {0}\r\n", new string('-', 100));
                svcInfo += String.Format("  - Servicename        : {0,-15} - Displayname      : {1}\r\n", sc.ServiceName, sc.DisplayName);
                svcInfo += String.Format("  - Can Stop           : {0,-15} - Status           : {1}\r\n", sc.CanStop, sc.Status);
                svcInfo += String.Format("  - Can Shutdown       : {0,-15} - Service Type     : {1}\r\n", sc.CanShutdown, sc.ServiceType);
                svcInfo += String.Format("  - Can Pause/Continue : {0,-15} - Startup Type     : {1}\r\n", sc.CanPauseAndContinue, startType);
                Console.WriteLine(svcInfo);
                sc.Dispose();
            }
            catch (Exception) { Console.WriteLine(ProjectInstaller.SvcServiceName + " is not installed as service"); }
            return;
        }

        // ====================================================================================================================
        //  WinFCU Show Version - Show the WinFCU version & license information
        // --------------------------------------------------------------------------------------------------------------------
        public static void ShowVersion()
        {
            string cliVersion = FileVersionInfo.GetVersionInfo(System.IO.Path.Combine(fcuPath, "Total.CLI.dll")).FileVersion;
            string utlVersion = FileVersionInfo.GetVersionInfo(System.IO.Path.Combine(fcuPath, "Total.Util.dll")).FileVersion;
            string l4nVersion = FileVersionInfo.GetVersionInfo(System.IO.Path.Combine(fcuPath, "log4net.dll")).FileVersion;
            Console.WriteLine("WinFCU (tm) version \"" + fcuVersion + "\"\r\n----------------------------------------");
            Console.WriteLine(" Total.CLI (tm)         - Total Productions CLI class library (build " + cliVersion + ")");
            Console.WriteLine(" Total.Util (tm)        - Total Productions Utils class library (build " + utlVersion + ")");
            Console.WriteLine(" log4net (tm)           - Apache log4net logging class library (build " + l4nVersion + ")");
        }

        // ====================================================================================================================
        //  WinFCU License - Show the WinFCU version & license information
        // --------------------------------------------------------------------------------------------------------------------
        public static void ShowLicense()
        {
            string versionText = @"

Total Productions - WinFCU (tm) {0}

Copyright (C) 2016-{1} Hans van Veen, Total Productions

   Licensed under the Apache License, Version 2.0 (the ""License""); you may not use this
   file except in compliance with the License.    You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software distributed under
   the License is distributed on an ""AS IS"" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
   ANY KIND, either express or implied. See the License for the specific language governing
   permissions and limitations under the License.
";
            Console.WriteLine(String.Format(versionText, fcuVersion, curYear));
        }

        // ====================================================================================================================
        //  WinFCU Show Help - Show the WinFCU help information
        // --------------------------------------------------------------------------------------------------------------------
        public static void ShowHelp()
        {
            string helpText = @"
 Total Productions - WinFCU (tm) {0}

 Usage: WinFCU [Options]...

 WinFCU moves, removes, compacts or archives files matching the specifications and instructions defined in
 the WinFCU configuration file (WinFCU.exe.config) which must be located in the same folder as WinFCU.exe
 Via include directives, additional configuration files can be loaded.

 WinFCU command line parameters and possible aliases are;

 Informational:
   -Help (-?)                Show this help information

   -Export [filename]        Exports the active configuration to the specified export file
                             (Default is .\WinFCU_Export.config)

   -Show Keywords            Show all replacement keywords + description available to WinFCU
   -Show License             Show the WinFCU open source software license info (ASF)
   -Show Schedule            Show the WinFCU run schedule(s) (when available)
   -Show Service             Show the WinFCUService details (when available)
   -Show Status              Show the WinFCU detailed status info
   -Show Version             Show the WinFCU version information

 Runtime:
   -[no]LogFile 'filename'   Send WinFCU logging to specified file
                             Can explicitly be negated (-noLogFile), console logging will continue!

   -Schedule 'schedule'      Run WinFCU using the configuration for schedule 'schedule'
                             Just running WinFCU will process all available schedules!

   -[no]Debug/-[no]Verbose   Show debug information when running (overrules the setting in the config file)
                             Can explicitly be negated (-noDebug or -noVerbose)

   -[no]Dryrun/-[no]WhatIf   Show what would have happend (overrules the setting in the config file)
                             Can explicitly be negated (-noDryrun or -noWhatIf)

 Service:
   -service [un]install      Install (or uninstall) WinFCU as service (requires elevation)
   -service stop/[re]start   Stop, Start or Restart the WinFCU service (requires elevation)
   -service status           Show the WinFCU service status (Not Installed, Stopped, Running, etc.)

 For details on the various qualifiers and the content of the configuration file please refer to the documentation.

 Copyright (C) 2016-{1} Hans van Veen, Total Productions
";
            Console.WriteLine(String.Format(helpText, fcuVersion, curYear));
        }

        // ====================================================================================================================
        //  WinFCU Show Status - Show the WinFCU status (config settings, is service installed? etc.)
        // --------------------------------------------------------------------------------------------------------------------
        public static void ShowStatus()
        {
            Console.WriteLine(GetStatus());
        }

        // ====================================================================================================================
        //  WinFCU Show Schedule - Show the WinFCU schedule details for service run schedule (requires config to be loaded!)
        // --------------------------------------------------------------------------------------------------------------------
        public static void ShowSchedule()
        {
            evtSch.ShowSchedule();
        }

        // ====================================================================================================================
        //  WinFCU Get Status - Get/Assemble the WinFCU status (config settings, is service installed? etc.)
        // --------------------------------------------------------------------------------------------------------------------
        public static string GetStatus()
        {
            string sc_Status;
            try { ServiceController sc = new ServiceController(ProjectInstaller.SvcServiceName); sc_Status = sc.Status.ToString(); sc.Dispose(); }
            catch (Exception) { sc_Status = "No Service"; }
            // --------------------------------------------------------------------------------------------------------------------
            //   Lets put all info into the prcInfo string so it will be written to the logfile when the config is (re)loaded
            // --------------------------------------------------------------------------------------------------------------------
            string prcInfo = String.Format(" WinFCU {0} - Run Details:\r\n{1}\r\n", fcuVersion, new string('=', 80));
            prcInfo += String.Format("\r\n WinFCU Process Info\r\n {0}\r\n", new string('-', 78));
            prcInfo += String.Format("  - Process ID           : {0,-12}- ParentID             : {1}\r\n", total.PRC.ProcessID, total.PRC.ParentID);
            prcInfo += String.Format("  - Logon Type           : {0,-12}- Logon Type Name      : {1}\r\n", total.PRC.Type, total.PRC.TypeName);
            prcInfo += String.Format("  - x64bit OS            : {0,-12}- x64bit Mode          : {1}\r\n", total.ENV.x64os, total.ENV.x64mode);

            prcInfo += String.Format("\r\n WinFCU Runtime Options\r\n {0}\r\n", new string('-', 78));
            prcInfo += String.Format("  - Debug                : {0,-12}- Dryrun               : {1}\r\n", total.APP.Debug, total.APP.Dryrun);
            prcInfo += String.Format("  - Parallel Schedules   : {0,-12}- Service              : {1}\r\n", fcu.parallelSchedules, sc_Status);

            prcInfo += String.Format("\r\n WinFCU Inheritable Defaults\r\n {0}\r\n", new string('-', 78));
            prcInfo += String.Format("  - File Age (days)      : {0,-12}- File Age check type  : {1}\r\n", defAttributes.fileAge, checkType[defAttributes.checkType]);
            prcInfo += String.Format("  - Process hidden files : {0,-12}- Delete empty folders : {1}\r\n", defAttributes.processHiddenFiles, defAttributes.deleteEmptyFolders);
            prcInfo += String.Format("  - Force overwrite      : {0,-12}- Recursive File Scan  : {1}\r\n", defAttributes.forceOverWrite, defAttributes.recursiveScan);
            prcInfo += String.Format("  - Allowed Systems      : {0,-12}- Default schedule     : {1}\r\n", defAttributes.systemName, defAttributes.scheduleName);
            prcInfo += String.Format("  - Internal ArchivePath : {0,-12}- Exclude from scan    : {1}\r\n", defAttributes.archivePath, defAttributes.excludeFromScan);

            prcInfo += String.Format("\r\n WinFCU Configuration Info\r\n {0}\r\n", new string('-', 78));
            prcInfo += String.Format("  - Logfile              : {0}\r\n", fcu.runLogFile);
            prcInfo += String.Format("  - Config file          : {0}\r\n", fcu.runConfig);
            string showInclude = "  - Include file(s)      : {0}\r\n";
            foreach (string incFile in fcu.includeFiles)
            {
                prcInfo += String.Format(showInclude, incFile);
                showInclude = "                           {0}\r\n";
            }

            prcInfo += String.Format("\r\n WinFCU Restricted Paths\r\n {0}\r\n", new string('-', 78));
            string showResPath = "  - Restricted path(s)   : {0}\r\n";
            foreach (string resPath in restrictedPaths)
            {
                prcInfo += String.Format(showResPath, resPath);
                showResPath = "                           {0}\r\n";
            }

            prcInfo += String.Format("\r\n{0}", new string('=', 80));
            return prcInfo;
        }

    }
}
