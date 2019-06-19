using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Total.Util
{
    // ----------------------------------------------------------------------------------------------------------------------------
    //   This file contains all keyword related functions. The main function is the ReplaceKeyword function, which will replace
    //   all keywords in a string. Another important/nice function is ShowKeywords which shows all currently available keywords
    // ----------------------------------------------------------------------------------------------------------------------------
    public partial class total
    {
        // ------------------------------------------------------------------------------------------------------------------------
        //   InitializeReplacementKeywordList - Intializes the replacement keyword list
        // ------------------------------------------------------------------------------------------------------------------------
        public static void InitializeReplacementKeywordList()
        {
            // Clear the list before filling it
            ReplacementKeywords.Clear();

            // Update the date and time variables
            DTM.Year = String.Format("{0:yyyy}", DateTime.Now);
            DTM.Month = String.Format("{0:yyyyMM}", DateTime.Now);
            DTM.Date = String.Format("{0:yyyyMMdd}", DateTime.Now);
            DTM.Time = String.Format("{0:HHmmss}", DateTime.Now);
            DTM.DateTime = String.Format("{0:yyyyMMddHHmmss}", DateTime.Now);

            // Add the default keywords and values
            AddReplacementKeyword("#APP#", APP.Fullname, "GENERIC: Fullname of current application");
            AddReplacementKeyword("#APPNAME#", APP.Name, "GENERIC: Internal productname of current application");
            AddReplacementKeyword("#APPDIR#", APP.Path, "GENERIC: Absolute path of the current application");
            AddReplacementKeyword("#APPPATH#", APP.Path, "GENERIC: Absolute path of the current application");
            AddReplacementKeyword("#VERSION#", APP.Version, "GENERIC: File version of the current application");
            AddReplacementKeyword("#DATE#", DTM.Date, "GENERIC: Current date in 'yyyyMMdd' format");
            AddReplacementKeyword("#TIME#", DTM.Time, "GENERIC: Current time in 'HHmmss' format");
            AddReplacementKeyword("#DATETIME#", DTM.DateTime, "GENERIC: Current date and time in 'yyyyMMddHHmmss' format");
            AddReplacementKeyword("#MONTH#", DTM.Month, "GENERIC: Current month in 'yyyyMM' format");
            AddReplacementKeyword("#YEAR#", DTM.Year, "GENERIC: Current year in 'yyyy' format");
            AddReplacementKeyword("#SERVER#", ENV.Server, "GENERIC: Name of current computer (ENV:COMPUTERNAME)");
            AddReplacementKeyword("#FQDN#", ENV.FQDN, "GENERIC: FQDN of current computer (DNS hostentry)");
            AddReplacementKeyword("#USRDMN#", ENV.UserDomain, "GENERIC: User domain (ENV:USERDOMAIN)");
            AddReplacementKeyword("#DOMAIN#", ENV.Domain, "GENERIC: User DNS domain (ENV:USERDNSDOMAIN)");
            AddReplacementKeyword("#SYSDRIVE#", ENV.SysDrive, "GENERIC: System drive (ENV:SYSTEMDRIVE)");
            AddReplacementKeyword("#SYSROOT#", ENV.SysRoot, "GENERIC: The 'active' System32 directory (32-bit path for 32-bit processes and 64-bit path for 64-bit processes)");
            AddReplacementKeyword("#WINDIR#", ENV.WinDir, "GENERIC: Windows directory (Windows path)");
            AddReplacementKeyword("#SYS32DIR#", ENV.System32Dir, "GENERIC: Native System32 path (64-bit path for both 32- and 64-bit processes)");
            AddReplacementKeyword("#PROGDATA#", ENV.ProgramData, "GENERIC: ProgramData folder on the system drive");
            AddReplacementKeyword("#PROGFILES#", ENV.ProgramFiles, "GENERIC: The 'active' ProgramFiles directory (32-bit path for 32-bit processes and 64-bit path for 64-bit processes)");
            AddReplacementKeyword("#PROGFILESX86#", ENV.ProgramFiles86, "GENERIC: The 32-bit ProgramFiles directory");
            AddReplacementKeyword("#USERSDIR#", ENV.UsersDir, "GENERIC: The Users root folder on the system drive");
            AddReplacementKeyword("#USERDATA#", ENV.UserData, "GENERIC: The process Local AppData folder");
            AddReplacementKeyword("#APPDATA#", ENV.AppData, "GENERIC: The process Roaming AppData folder");
            AddReplacementKeyword("#PUBLIC#", ENV.PublicDir, "GENERIC: The Public folder in the users root folder");
            AddReplacementKeyword("#TEMP#", ENV.Temp, "GENERIC: Temp folder path for the current process");
            AddReplacementKeyword("#TMP#", ENV.Tmp, "GENERIC: Temp folder path for the current process");
            AddReplacementKeyword("#ERRLOG#", APP.ErrLog, "GENERIC: The application specific error logfile ('AppName'_error.log)");
            AddReplacementKeyword("#LOGFILE#", APP.LogFile, "GENERIC: The application specific logfile ('AppName'.log)");
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   AddReplacementKeyword - Adds a new (or updates an existing) replacement keyword record
        // ------------------------------------------------------------------------------------------------------------------------
        public static void AddReplacementKeyword(string keyWord, string keywordValue, string keywordDescription = " --- : # No Description Available #")
        {
            string[] keywordData = new string[2];
            keywordData[0] = keywordValue;
            keywordData[1] = keywordDescription;
            if (ReplacementKeywords.ContainsKey(keyWord)) { ReplacementKeywords.Remove(keyWord); }
            ReplacementKeywords.Add(keyWord, keywordData);
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Replace all known keywords in the provided string by their run-time value. Some values (like date & time) are
        //   'dynamic' and must refreshed before use.
        // ------------------------------------------------------------------------------------------------------------------------
        public static string ReplaceKeyword(string SourceString)
        {
            string Record = SourceString.ToString();
            // --------------------------------------------------------------------------------------------------------------------
            //   Get the current date and time values and update the keyword list.
            // --------------------------------------------------------------------------------------------------------------------
            DTM.Year = String.Format("{0:yyyy}", DateTime.Now);
            DTM.Month = String.Format("{0:yyyyMM}", DateTime.Now);
            DTM.Date = String.Format("{0:yyyyMMdd}", DateTime.Now);
            DTM.Time = String.Format("{0:HHmmss}", DateTime.Now);
            DTM.DateTime = String.Format("{0:yyyyMMddHHmmss}", DateTime.Now);

            AddReplacementKeyword("#DATE#", DTM.Date, "GENERIC: Current date in 'yyyyMMdd' format");
            AddReplacementKeyword("#TIME#", DTM.Time, "GENERIC: Current time in 'HHmmss' format");
            AddReplacementKeyword("#DATETIME#", DTM.DateTime, "GENERIC: Current date and time in 'yyyyMMddHHmmss' format");
            AddReplacementKeyword("#MONTH#", DTM.Month, "GENERIC: Current month in 'yyyyMM' format");
            AddReplacementKeyword("#YEAR#", DTM.Year, "GENERIC: Current year in 'yyyy' format");
            // --------------------------------------------------------------------------------------------------------------------
            //   Now replace known keywords in the input string
            // --------------------------------------------------------------------------------------------------------------------
            foreach (string keyWord in ReplacementKeywords.Keys)
            {
                try
                {
                    string keywordValue = ReplacementKeywords[keyWord][0].ToString();
                    Record = Record.Replace(keyWord, keywordValue);
                }
                catch { Logger.Warn("Resolve Keyword Replacement Exception on: " + keyWord); }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Return the 'converted' string
            // --------------------------------------------------------------------------------------------------------------------
            return Record;
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Replace all known keywords in the provided string by their run-time value. Some values (like date & time) are
        //   'dynamic' and must refreshed before use.
        // ------------------------------------------------------------------------------------------------------------------------
        public static void ShowReplacementKeywords(string productName = "Total Productions")
        {
            // --------------------------------------------------------------------------------------------------------------------
            //   Get the current date and time values and update the keyword list.
            // --------------------------------------------------------------------------------------------------------------------
            DTM.Year = String.Format("{0:yyyy}", DateTime.Now);
            DTM.Month = String.Format("{0:yyyyMM}", DateTime.Now);
            DTM.Date = String.Format("{0:yyyyMMdd}", DateTime.Now);
            DTM.Time = String.Format("{0:HHmmss}", DateTime.Now);
            DTM.DateTime = String.Format("{0:yyyyMMddHHmmss}", DateTime.Now);

            AddReplacementKeyword("#DATE#", DTM.Date, "GENERIC: Current date in 'yyyyMMdd' format");
            AddReplacementKeyword("#TIME#", DTM.Time, "GENERIC: Current time in 'HHmmss' format");
            AddReplacementKeyword("#DATETIME#", DTM.DateTime, "GENERIC: Current date and time in 'yyyyMMddHHmmss' format");
            AddReplacementKeyword("#MONTH#", DTM.Month, "GENERIC: Current month in 'yyyyMM' format");
            AddReplacementKeyword("#YEAR#", DTM.Year, "GENERIC: Current year in 'yyyy' format");
            // --------------------------------------------------------------------------------------------------------------------
            //   Now show all known keywords, application static and user
            // --------------------------------------------------------------------------------------------------------------------
            Console.WriteLine(String.Format("\r\n {0} Replacement Keywords\r\n{1}", productName, new string('-', 132)));
            foreach (string keyWord in ReplacementKeywords.Keys)
            {
                if (keyWord.Length < 1) { continue; }
                Console.WriteLine(String.Format("  {0,-20} - {1} [Value: {2}]", keyWord, ReplacementKeywords[keyWord][1].ToString(), ReplacementKeywords[keyWord][0].ToString()));
            }
        }


    }
    // ----------------------------------------------------------------------------------------------------------------------------
    // End of class
    // ----------------------------------------------------------------------------------------------------------------------------
}
// ================================================================================================================================
//    EOF, Sayonara!
// ================================================================================================================================