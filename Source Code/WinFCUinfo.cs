using System;
using System.Diagnostics;
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
        private static string fcuVersion  = FileVersionInfo.GetVersionInfo(fcuFilename).FileVersion;
        // ====================================================================================================================
        //  WinFCU Version - Show the WinFCU version & license information
        // --------------------------------------------------------------------------------------------------------------------
        public static void ShowVersion()
        {
            string cliVersion = FileVersionInfo.GetVersionInfo(System.IO.Path.Combine(fcuPath, "Total.CLI.dll")).FileVersion;
            string l4nVersion = FileVersionInfo.GetVersionInfo(System.IO.Path.Combine(fcuPath, "log4net.dll")).FileVersion;
            string sbpVersion = FileVersionInfo.GetVersionInfo(System.IO.Path.Combine(fcuPath, "Total.Util.dll")).FileVersion;
            Console.WriteLine("WinFCU (tm) version \"" + fcuVersion + "\"");
            Console.WriteLine(" Total.CLI (tm)    - Command Line Interpreter class library (build " + cliVersion + ")");
            Console.WriteLine(" Total.Util (tm)   - common functions class library (build " + sbpVersion + ")");
            Console.WriteLine(" log4net (tm)      - part of the Apache Logging Services (build " + l4nVersion + ")");
        }

        // ====================================================================================================================
        //  WinFCU License - Show the WinFCU version & license information
        // --------------------------------------------------------------------------------------------------------------------
        public static void ShowLicense()
        {
            string versionText = @"

Total Productions - WinFCU (tm) {0}

Copyright (C) 2016 Hans van Veen, Total Productions

   Licensed under the Apache License, Version 2.0 (the ""License""); you may not use this
   file except in compliance with the License.    You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software distributed under
   the License is distributed on an ""AS IS"" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
   ANY KIND, either express or implied. See the License for the specific language governing
   permissions and limitations under the License.
";
            Console.WriteLine(String.Format(versionText, fcuVersion));
        }

        // ====================================================================================================================
        //  WinFCU Help - Show the WinFCU help information
        // --------------------------------------------------------------------------------------------------------------------
        public static void ShowHelp()
        {
            string helpText = @"
 Total Productions - WinFCU (tm) {0}

 Usage: WinFCU [Options]...

 WinFCU moves, removes, compacts or archives files matching the specifications and instructions defined in
 the WinFCU configuration file (WinFCU.exe.config) which must be located in the same folder as WinFCU.exe
 Via include directives, additional configuration files can be loaded.

 WinFCU commandline parameters and possible aliases are;

 Informational:
   Help [-?]            Show this help information

   Export               Exports the active configuration to the specified export file
                        (Default is .\WinFCU_Export.config)

   Show License         Show the WinFCU open source software license info (ASF)
   Show Version         Show the WinFCU version information
   Show Status          Show the WinFCU status
   Show Schedule        Show the WinFCU run schedule(s)

 Runtime:
   [no]LogFile 'filename'  Send WinFCU logging to specified file
                           Can explicitly be negated (-noLogFile), console logging will continue!

   Schedule 'schedule'     Run WinFCU using the configuration for schedule 'schedule'
                           Just running WinFCU will use all available schedules!

   [no]Debug/[no]Verbose   Show debug information when running (overrules the setting in the config file)
                           Can explicitly be negated (-noDebug or -noVerbose)

   [no]Dryrun/[no]WhatIf   Show what would have happend (overrules the setting in the config file)
                           Can explicitly be negated (-noDryrun or -noWhatIf)

 For details on the various qualifiers and the content of the configuration file please refer to the documentation.

 Copyright (C) 2015 Hans van Veen, Total Productions
";
            Console.WriteLine(String.Format(helpText, fcuVersion));
        }

        // ====================================================================================================================
        //  WinFCU Status - Show the WinFCU status (config settings, is service installed? etc.)
        // --------------------------------------------------------------------------------------------------------------------
        public static void ShowStatus()
        {
            Console.WriteLine(prcInfo);
        }

        // ====================================================================================================================
        //  WinFCU Schedule - Show the WinFCU schedule details for service run schedule (requires config to be loaded!)
        // --------------------------------------------------------------------------------------------------------------------
        public static void ShowSchedule()
        {
            total.ShowSchedule();
        }

    }
}
