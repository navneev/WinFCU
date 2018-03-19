using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Xml;

namespace Total.WinFCU
{
    public partial class fcu
    {
        // application configuration file
        public static Configuration appConfig;

        // Counter stuff
        public static long  folder_bytesZipped,    total_bytesZipped = 0;
        public static float folder_CompressRatio,  total_CompressRatio = 0;
        public static long  folder_bytesCompacted, total_bytesCompacted = 0;
        public static float folder_CompationRatio, total_CompactionRatio = 0;
        public static long  folder_bytesArchived,  total_bytesArchived = 0;
        public static long  folder_bytesDeleted,   total_bytesDeleted = 0;
        public static long  folder_bytesMoved,     total_bytesMoved = 0;

        // RunOptions stuff
        public static bool         runHasSchedule         { get { return run_HasSchedule; }         set { run_HasSchedule = value; } }        private static bool         run_HasSchedule;
        public static string       runLogFile             { get { return run_LogFile; }             set { run_LogFile = value; } }            private static string       run_LogFile;
        public static string       runConfig              { get { return run_Config; }              set { run_Config = value; } }             private static string       run_Config;
        public static List<String> incConfig              { get { return inc_Config; }              set { inc_Config = value; } }             private static List<String> inc_Config = new List<String>();
        // Security stuff
        public static string       secAccount             { get { return sec_account; }             set { sec_account = value; } }            private static string       sec_account;
        public static string       secPassword            { get { return sec_password; }            set { sec_password = value; } }           private static string       sec_password;
        public static string       secCredentials         { get { return sec_credentials; }         set { sec_credentials = value; } }        private static string       sec_credentials;
        // xml config stuff
        public static XmlDocument  fcuConfig              { get { return fcu_config; }              set { fcu_config = value; } }             private static XmlDocument  fcu_config;
        public static XmlNodeList  fcuFolders             { get { return fcu_folders; }             set { fcu_folders = value; } }            private static XmlNodeList  fcu_folders;
        // restricted paths
        public static List<string> restrictedPaths = new List<string>();

        public struct scanAttributes
        {
            public bool   processHiddenFiles;            // Inheritable 
            public bool   deleteEmptyFolders;            // Inheritable
            public bool   forceOverWrite;                // Inheritable
            public bool   recursiveScan;                 // Inheritable
            public char   checkType;                     // Inheritable
            public string scheduleName;                  // Inheritable
            public string fileName;                      // Not Inheritable
            public string fileAge;                       // Inheritable
            public string actionName;                    // Not Inheritable
            public string actionTarget;                  // Not Inheritable
            public Regex  systemName;                    // Inheritable
            public Regex  excludeFromScan;               // Not Inheritable
        }
        public struct INF
        {
            public static int      fileDirCount = 0;
            public static string   scheduleName;
            public static string   filePath;
            public static string   fileName;
            public static string   fileBaseName;
            public static string   fileExt;
            public static string[] fileRdir = new string[10];
            public static string[] fileSdir = new string[10];
            public static string   createDate;
            public static string   createMonth;
            public static string   createYear;
            public static string   modifyDate;
            public static string   modifyMonth;
            public static string   modifyYear;
            public static string   accessDate;
            public static string   accessMonth;
            public static string   accessYear;
        }

    }
}
// ================================================================================================================================
//    End of partial class fcu (data), Sayonara!
// ================================================================================================================================