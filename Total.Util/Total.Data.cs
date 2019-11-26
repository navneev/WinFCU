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
            public static Configuration    Configuration    { get { return _Configuration; }    set { _Configuration = value; } }     private static Configuration _Configuration;
            public static string           Name             { get { return _Name; }             set { _Name = value; } }              private static string   _Name;
            public static string           Version          { get { return _Version; }          set { _Version = value; } }           private static string   _Version;
            public static FileVersionInfo  File             { get { return _File; }             set { _File = value; } }              private static FileVersionInfo _File;
            public static FileInfo         FileInfo         { get { return _FileInfo; }         set { _FileInfo = value; } }          private static FileInfo _FileInfo;
            public static string           Fullname         { get { return _Fullname; }         set { _Fullname = value; } }          private static string   _Fullname;
            public static string           Config           { get { return _Config; }           set { _Config = value; } }            private static string   _Config;
            public static string           Path             { get { return _Path; }             set { _Path = value; } }              private static string   _Path;
            public static string           Filename         { get { return _Filename; }         set { _Filename = value; } }          private static string   _Filename;
            public static string           Fileext          { get { return _Fileext; }          set { _Fileext = value; } }           private static string   _Fileext;
            public static string           LogFile          { get { return _LogFile; }          set { _LogFile = value; } }           private static string   _LogFile;
            public static string           PrcInfo          { get { return _PrcInfo; }          set { _PrcInfo = value; } }           private static string   _PrcInfo;
            public static string           ErrLog           { get { return _ErrLog; }           set { _ErrLog = value; } }            private static string   _ErrLog;
            public static bool             HasConfigFile    { get { return _HasConfigFile; }    set { _HasConfigFile = value; } }     private static bool     _HasConfigFile;
            public static bool             DoLog            { get { return _doLog; }            set { _doLog = value; } }             private static bool     _doLog;
            public static bool             Debug            { get { return _Debug; }            set { _Debug = value; } }             private static bool     _Debug;
            public static bool             Dryrun           { get { return _Dryrun; }           set { _Dryrun = value; } }            private static bool     _Dryrun;
        }

        // Environment related stuff
        public struct ENV
        {
            public static string           Server           { get { return _Server; }           set { _Server = value; } }            private static string   _Server;
            public static string           UserDomain       { get { return _UserDomain; }       set { _UserDomain = value; } }        private static string   _UserDomain;
            public static string           FQDN             { get { return _FQDN; }             set { _FQDN = value; } }              private static string   _FQDN;
            public static string           Domain           { get { return _Domain; }           set { _Domain = value; } }            private static string   _Domain;
            public static string           SysDrive         { get { return _SysDrive; }         set { _SysDrive = value; } }          private static string   _SysDrive;
            public static string           SysRoot          { get { return _SysRoot; }          set { _SysRoot = value; } }           private static string   _SysRoot;
            public static string           WinDir           { get { return _WinDir; }           set { _WinDir = value; } }            private static string   _WinDir;
            public static string           System32Dir      { get { return _System32Dir; }      set { _System32Dir = value; } }       private static string   _System32Dir;
            public static string           ProgramData      { get { return _ProgramData; }      set { _ProgramData = value; } }       private static string   _ProgramData;
            public static string           ProgramFiles     { get { return _ProgramFiles; }     set { _ProgramFiles = value; } }      private static string   _ProgramFiles;
            public static string           ProgramFiles86   { get { return _ProgramFiles86; }   set { _ProgramFiles86 = value; } }    private static string   _ProgramFiles86;
            public static string           UserProfile      { get { return _UserProfile; }      set { _UserProfile = value; } }       private static string   _UserProfile;
            public static string           UserData         { get { return _UserData; }         set { _UserData = value; } }          private static string   _UserData;
            public static string           AppData          { get { return _AppData; }          set { _AppData = value; } }           private static string   _AppData;
            public static string           UsersDir         { get { return _UsersDir; }         set { _UsersDir = value; } }          private static string   _UsersDir;
            public static string           PublicDir        { get { return _PublicDir; }        set { _PublicDir = value; } }         private static string   _PublicDir;
            public static string           Temp             { get { return _Temp; }             set { _Temp = value; } }              private static string   _Temp;
            public static string           Tmp              { get { return _Tmp; }              set { _Tmp = value; } }               private static string   _Tmp;
            public static bool             x64os            { get { return _x64os; }            set { _x64os = value; } }             private static bool     _x64os;
            public static bool             x64mode          { get { return _x64mode; }          set { _x64mode = value; } }           private static bool     _x64mode;
        }

        // Environment related stuff
        public struct SYS
        {
            public static bool             longpathenabled  { get { return _longpathenabled; }  set { _longpathenabled = value; } }   private static bool     _longpathenabled;
        }

        // Process related stuff  
        public struct PRC
        {
            public static Process          Process          { get { return _Process; }          set { _Process = value; } }           private static Process  _Process;
            public static Process          Parent           { get { return _Parent; }           set { _Parent = value; } }            private static Process  _Parent;
            public static int              ProcessID        { get { return _ProcessID; }        set { _ProcessID = value; } }         private static int      _ProcessID;
            public static int              ParentID         { get { return _ParentID; }         set { _ParentID = value; } }          private static int      _ParentID;
            public static string           LogonID          { get { return _LogonID; }          set { _LogonID = value; } }           private static string   _LogonID;
            public static string           TypeName         { get { return _TypeName; }         set { _TypeName = value; } }          private static string   _TypeName;
            public static int              Type             { get { return _Type; }             set { _Type = value; } }              private static int      _Type;
            public static bool             Interactive      { get { return _Interactive; }      set { _Interactive = value; } }       private static bool     _Interactive;
            public static bool             Network          { get { return _Network; }          set { _Network = value; } }           private static bool     _Network;
            public static bool             Batch            { get { return _Batch; }            set { _Batch = value; } }             private static bool     _Batch;
            public static bool             Service          { get { return _Service; }          set { _Service = value; } }           private static bool     _Service;
        }

        // Date & Time related stuff
        public struct DTM
        {
            public static DateTime         Start            { get { return _Start; }            set { _Start = value; } }             public static DateTime  _Start;
            public static string           DateFormat       { get { return _DateFmt; }          set { _DateFmt = value; } }           private static string   _DateFmt;
            public static string           DateSeparator    { get { return _DateSep; }          set { _DateSep = value; } }           private static string   _DateSep;
            public static string           TimeFormat       { get { return _TimeFmt; }          set { _TimeFmt = value; } }           private static string   _TimeFmt;
            public static string           TimeSeparator    { get { return _TimeSep; }          set { _TimeSep = value; } }           private static string   _TimeSep;
            public static string           Format           { get { return _Format; }           set { _Format = value; } }            private static string   _Format;
            public static string           Year             { get { return _Year; }             set { _Year = value; } }              private static string   _Year;
            public static string           Month            { get { return _Month; }            set { _Month = value; } }             private static string   _Month;
            public static string           Date             { get { return _Date; }             set { _Date = value; } }              private static string   _Date;
            public static string           Time             { get { return _Time; }             set { _Time = value; } }              private static string   _Time;
            public static string           DateTime         { get { return _DateTime; }         set { _DateTime = value; } }          private static string   _DateTime;
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
            public static string           ServiceName      { get { return _servicename; }      set { _servicename = value; } }       private static string   _servicename;
            public static string           Description      { get { return _description; }      set { _description = value; } }       private static string   _description;
            public static string           EventLogName     { get { return _eventlogname; }     set { _eventlogname = value; } }      private static string   _eventlogname;
            public static string           DisplayName      { get { return _displayname; }      set { _displayname = value; } }       private static string   _displayname;
            public static ServiceStartMode StartupType      { get { return _startuptype; }      set { _startuptype = value; } }       private static ServiceStartMode _startuptype;
            public static string           BinaryPath       { get { return _binarypath; }       set { _binarypath = value; } }        private static string   _binarypath;
            public static bool             PowerEvent       { get { return _powerevent; }       set { _powerevent = value; } }        private static bool     _powerevent;
            public static bool             ChangeEvent      { get { return _changeevent; }      set { _changeevent = value; } }       private static bool     _changeevent;
            public static bool             CanPause         { get { return _canpause; }         set { _canpause = value; } }          private static bool     _canpause;
            public static bool             Shutdown         { get { return _shutdown; }         set { _shutdown = value; } }          private static bool     _shutdown;
            public static bool             CanStop          { get { return _canstop; }          set { _canstop = value; } }           private static bool     _canstop;
            public static ServiceAccount   Account          { get { return _account; }          set { _account = value; } }           private static ServiceAccount _account;
            public static string           Username         { get { return _username; }         set { _username = value; } }          private static string   _username;
            public static string           Password         { get { return _password; }         set { _password = value; } }          private static string   _password;
        }

    }
}
// ================================================================================================================================================================================
//    End of partial class app (data), Sayonara!
// ================================================================================================================================================================================