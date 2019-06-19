using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using log4net;

namespace Total.Util
{
    public partial class total
    {
        // Prepare for log4net logger
        public static log4net.ILog Logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // User defined keywords
        public static SortedList<string, string[]> ReplacementKeywords = new SortedList<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Application related stuff
        public struct APP
        {
            public static Configuration    Configuration    { get { return app_Configuration; }    set { app_Configuration = value; } }     private static Configuration app_Configuration;
            public static string           Name             { get { return app_Name; }             set { app_Name = value; } }              private static string   app_Name;
            public static string           Version          { get { return app_Version; }          set { app_Version = value; } }           private static string   app_Version;
            public static FileVersionInfo  File             { get { return app_File; }             set { app_File = value; } }              private static FileVersionInfo app_File;
            public static FileInfo         FileInfo         { get { return app_FileInfo; }         set { app_FileInfo = value; } }          private static FileInfo app_FileInfo;
            public static string           Fullname         { get { return app_Fullname; }         set { app_Fullname = value; } }          private static string   app_Fullname;
            public static string           Config           { get { return app_Config; }           set { app_Config = value; } }            private static string   app_Config;
            public static string           Path             { get { return app_Path; }             set { app_Path = value; } }              private static string   app_Path;
            public static string           Filename         { get { return app_Filename; }         set { app_Filename = value; } }          private static string   app_Filename;
            public static string           Fileext          { get { return app_Fileext; }          set { app_Fileext = value; } }           private static string   app_Fileext;
            public static string           LogFile          { get { return app_LogFile; }          set { app_LogFile = value; } }           private static string   app_LogFile;
            public static string           PrcInfo          { get { return app_PrcInfo; }          set { app_PrcInfo = value; } }           private static string   app_PrcInfo;
            public static string           ErrLog           { get { return app_ErrLog; }           set { app_ErrLog = value; } }            private static string   app_ErrLog;
            public static bool             HasConfigFile    { get { return app_HasConfigFile; }    set { app_HasConfigFile = value; } }     private static bool     app_HasConfigFile;
            public static bool             DoLog            { get { return app_doLog; }            set { app_doLog = value; } }             private static bool     app_doLog;
            public static bool             Debug            { get { return app_Debug; }            set { app_Debug = value; } }             private static bool     app_Debug;
            public static bool             Dryrun           { get { return app_Dryrun; }           set { app_Dryrun = value; } }            private static bool     app_Dryrun;
        }

        // Environment related stuff
        public struct ENV
        {
            public static string           Server           { get { return env_Server; }           set { env_Server = value; } }            private static string   env_Server;
            public static string           UserDomain       { get { return env_UserDomain; }       set { env_UserDomain = value; } }        private static string   env_UserDomain;
            public static string           FQDN             { get { return env_FQDN; }             set { env_FQDN = value; } }              private static string   env_FQDN;
            public static string           Domain           { get { return env_Domain; }           set { env_Domain = value; } }            private static string   env_Domain;
            public static string           SysDrive         { get { return env_SysDrive; }         set { env_SysDrive = value; } }          private static string   env_SysDrive;
            public static string           SysRoot          { get { return env_SysRoot; }          set { env_SysRoot = value; } }           private static string   env_SysRoot;
            public static string           WinDir           { get { return env_WinDir; }           set { env_WinDir = value; } }            private static string   env_WinDir;
            public static string           System32Dir      { get { return env_System32Dir; }      set { env_System32Dir = value; } }       private static string   env_System32Dir;
            public static string           ProgramData      { get { return env_ProgramData; }      set { env_ProgramData = value; } }       private static string   env_ProgramData;
            public static string           ProgramFiles     { get { return env_ProgramFiles; }     set { env_ProgramFiles = value; } }      private static string   env_ProgramFiles;
            public static string           ProgramFiles86   { get { return env_ProgramFiles86; }   set { env_ProgramFiles86 = value; } }    private static string   env_ProgramFiles86;
            public static string           UserProfile      { get { return env_UserProfile; }      set { env_UserProfile = value; } }       private static string   env_UserProfile;
            public static string           UserData         { get { return env_UserData; }         set { env_UserData = value; } }          private static string   env_UserData;
            public static string           AppData          { get { return env_AppData; }          set { env_AppData = value; } }           private static string   env_AppData;
            public static string           UsersDir         { get { return env_UsersDir; }         set { env_UsersDir = value; } }          private static string   env_UsersDir;
            public static string           PublicDir        { get { return env_PublicDir; }        set { env_PublicDir = value; } }         private static string   env_PublicDir;
            public static string           Temp             { get   { return env_Temp; }           set { env_Temp = value; } }              private static string   env_Temp;
            public static string           Tmp              { get { return env_Tmp; }              set { env_Tmp = value; } }               private static string   env_Tmp;
            public static bool             x64os            { get { return env_x64os; }            set { env_x64os = value; } }             private static bool     env_x64os;
            public static bool             x64mode          { get { return env_x64mode; }          set { env_x64mode = value; } }           private static bool     env_x64mode;
        }

        // Process related stuff  
        public struct PRC
        {
            public static Process          Process          { get { return prc_Process; }          set { prc_Process = value; } }           private static Process  prc_Process;
            public static Process          Parent           { get { return prc_Parent; }           set { prc_Parent = value; } }            private static Process  prc_Parent;
            public static int              ProcessID        { get { return prc_ProcessID; }        set { prc_ProcessID = value; } }         private static int      prc_ProcessID;
            public static int              ParentID         { get { return prc_ParentID; }         set { prc_ParentID = value; } }          private static int      prc_ParentID;
            public static string           LogonID          { get { return prc_LogonID; }          set { prc_LogonID = value; } }           private static string   prc_LogonID;
            public static string           TypeName         { get { return prc_TypeName; }         set { prc_TypeName = value; } }          private static string   prc_TypeName;
            public static int              Type             { get { return prc_Type; }             set { prc_Type = value; } }              private static int      prc_Type;
            public static bool             Interactive      { get { return prc_Interactive; }      set { prc_Interactive = value; } }       private static bool     prc_Interactive;
            public static bool             Network          { get { return prc_Network; }          set { prc_Network = value; } }           private static bool     prc_Network;
            public static bool             Batch            { get { return prc_Batch; }            set { prc_Batch = value; } }             private static bool     prc_Batch;
            public static bool             Service          { get { return prc_Service; }          set { prc_Service = value; } }           private static bool     prc_Service;
        }

        // Date & Time related stuff
        public struct DTM
        {
            public static DateTime         Start            { get { return dtm_Start; }            set { dtm_Start = value; } }             public static DateTime  dtm_Start;
            public static string           DateFormat       { get { return dtm_DateFmt; }          set { dtm_DateFmt = value; } }           private static string   dtm_DateFmt;
            public static string           DateSeparator    { get { return dtm_DateSep; }          set { dtm_DateSep = value; } }           private static string   dtm_DateSep;
            public static string           TimeFormat       { get { return dtm_TimeFmt; }          set { dtm_TimeFmt = value; } }           private static string   dtm_TimeFmt;
            public static string           TimeSeparator    { get { return dtm_TimeSep; }          set { dtm_TimeSep = value; } }           private static string   dtm_TimeSep;
            public static string           Format           { get { return dtm_Format; }           set { dtm_Format = value; } }            private static string   dtm_Format;
            public static string           Year             { get { return dtm_Year; }             set { dtm_Year = value; } }              private static string   dtm_Year;
            public static string           Month            { get { return dtm_Month; }            set { dtm_Month = value; } }             private static string   dtm_Month;
            public static string           Date             { get { return dtm_Date; }             set { dtm_Date = value; } }              private static string   dtm_Date;
            public static string           Time             { get { return dtm_Time; }             set { dtm_Time = value; } }              private static string   dtm_Time;
            public static string           DateTime         { get { return dtm_DateTime; }         set { dtm_DateTime = value; } }          private static string   dtm_DateTime;
        }
        
        // Archiver stuff
        public struct ARC
        {
            public string  Filename;
            public long    orgFileSize;
            public long    cmpFileSize;
            public decimal cmpRatio;
        }
        
        // Scheduler stuff
//        public struct SCH
//        {
//            public static string           Name             { get { return sch_Name; }             set { sch_Name = value; } }              private static string   sch_Name;
//            public static string           Days             { get { return sch_Days; }             set { sch_Days = value; } }              private static string   sch_Days;
//            public static string           Start            { get { return sch_Start; }            set { sch_Start = value; } }             private static string   sch_Start;
//            public static string           End              { get { return sch_End; }              set { sch_End = value; } }               private static string   sch_End;
//            public static string           Interval         { get { return sch_Interval; }         set { sch_Interval = value; } }          private static string   sch_Interval;
//            public static string           System           { get { return sch_System; }           set { sch_System = value; } }            private static string   sch_System;
//
//            public static SortedList       Schedule       = new SortedList(StringComparer.OrdinalIgnoreCase);
//            public static SortedList       nextRunTime    = new SortedList(StringComparer.OrdinalIgnoreCase);
//            public static SortedList       dayNumbers     = new SortedList(StringComparer.OrdinalIgnoreCase) { {"su",0}, {"mo",1}, {"tu",2}, {"we",3}, {"th",4}, {"fr",5}, {"sa",6},
//                                                                                                               {"ev","0 1 2 3 4 5 6"}, {"wd","1 2 3 4 5"}, {"wn","0 6"} };
//        }

        // Services related stuff
        public struct SVC
        {
            public static string           ServiceName      { get { return svc_servicename; }      set { svc_servicename = value; } }       private static string   svc_servicename;
            public static string           Description      { get { return svc_description; }      set { svc_description = value; } }       private static string   svc_description;
            public static string           EventLogName     { get { return svc_eventlogname; }     set { svc_eventlogname = value; } }      private static string   svc_eventlogname;
            public static string           DisplayName      { get { return svc_displayname; }      set { svc_displayname = value; } }       private static string   svc_displayname;
            public static ServiceStartMode StartupType      { get { return svc_startuptype; }      set { svc_startuptype = value; } }       private static ServiceStartMode svc_startuptype;
            public static string           BinaryPath       { get { return svc_binarypath; }       set { svc_binarypath = value; } }        private static string   svc_binarypath;
            public static bool             PowerEvent       { get { return svc_powerevent; }       set { svc_powerevent = value; } }        private static bool     svc_powerevent;
            public static bool             ChangeEvent      { get { return svc_changeevent; }      set { svc_changeevent = value; } }       private static bool     svc_changeevent;
            public static bool             CanPause         { get { return svc_canpause; }         set { svc_canpause = value; } }          private static bool     svc_canpause;
            public static bool             Shutdown         { get { return svc_shutdown; }         set { svc_shutdown = value; } }          private static bool     svc_shutdown;
            public static bool             CanStop          { get { return svc_canstop; }          set { svc_canstop = value; } }           private static bool     svc_canstop;
            public static ServiceAccount   Account          { get { return svc_account; }          set { svc_account = value; } }           private static ServiceAccount svc_account;
            public static string           Username         { get { return svc_username; }         set { svc_username = value; } }          private static string   svc_username;
            public static string           Password         { get { return svc_password; }         set { svc_password = value; } }          private static string   svc_password;
        }

    }
}
// ================================================================================================================================================================================
//    End of partial class app (data), Sayonara!
// ================================================================================================================================================================================