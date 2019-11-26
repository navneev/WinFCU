using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using log4net.Util;

namespace Total.Util
{
    public partial class total
    {
        // ========================================================================================================================
        //   Initialize the application run context by collecting runtime info.
        // ------------------------------------------------------------------------------------------------------------------------
        public static void InitializeContext()
        {
            // ====================================================================================================================
            //   Collect process information. Process Type 0 is a process running as a 'Local System Service'
            //   This type is used for both Service and Batch (TaskScheduler) processes, so lets be smart and....
            // --------------------------------------------------------------------------------------------------------------------
            PRC.Process     = Process.GetCurrentProcess();
            PRC.Parent      = GetParentProccess();
            PRC.ProcessID   = PRC.Process.Id;
            PRC.ParentID    = PRC.Parent.Id;
            PRC.Type        = GetProcessLogonType();
            // --------------------------------------------------------------------------------------------------------------------
            //  If the type is (still) 0 than something went wrong with the GetProcessLogonType() call.
            //  PRC.Type 14 or 15 is a Batch job or Service running under the local system account!
            // --------------------------------------------------------------------------------------------------------------------
            PRC.Interactive = PRC.Type == 2 || PRC.Type == 9 || PRC.Type == 10 || PRC.Type == 11;
            PRC.Network     = PRC.Type == 3 || PRC.Type == 8;
            PRC.Batch       = PRC.Type == 4 || PRC.Type == 14;
            PRC.Service     = PRC.Type == 5 || PRC.Type == 15;
            // ====================================================================================================================
            //   Collect the basic application info
            // --------------------------------------------------------------------------------------------------------------------
            APP.FileInfo      = new FileInfo(Application.ExecutablePath);
            APP.File          = FileVersionInfo.GetVersionInfo(Application.ExecutablePath);
            APP.Configuration = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath);
            APP.Name          = APP.File.ProductName;
            APP.Version       = APP.File.FileVersion;
            APP.Fullname      = APP.File.FileName;
            APP.Config        = APP.Fullname + ".config";
            APP.Path          = Path.GetDirectoryName(APP.Fullname);
            APP.Filename      = Path.GetFileName(APP.Fullname);
            APP.Fileext       = Path.GetExtension(APP.Fullname);
            APP.LogFile       = APP.Fullname.Replace(APP.Fileext, ".log");
            APP.ErrLog        = APP.Fullname.Replace(APP.Fileext, "_error.log");
            APP.HasConfigFile = APP.Configuration.HasFile;
            if (APP.HasConfigFile) { APP.Config = APP.Configuration.FilePath; }
            APP.Debug         = false;
            APP.Dryrun        = false;
            // ====================================================================================================================
            //   Add Environment related variables
            // --------------------------------------------------------------------------------------------------------------------
            ENV.Server         = Environment.MachineName;
            ENV.FQDN           = System.Net.Dns.GetHostEntry(ENV.Server).HostName.ToUpper();
            ENV.Domain         = IPGlobalProperties.GetIPGlobalProperties().DomainName;
            ENV.UserDomain     = Environment.UserDomainName;
            ENV.SysDrive       = Environment.GetEnvironmentVariable("SystemDrive");
            ENV.SysRoot        = Environment.SystemDirectory;
            ENV.WinDir         = Environment.GetEnvironmentVariable("WinDir");
            ENV.System32Dir    = ENV.WinDir + "\\SysNative";
            ENV.ProgramData    = Environment.GetEnvironmentVariable("ProgramData");
            ENV.ProgramFiles   = Environment.GetEnvironmentVariable("ProgramFiles");
            ENV.ProgramFiles86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            ENV.UserProfile    = Environment.GetEnvironmentVariable("UserProfile");
            ENV.PublicDir      = Environment.GetEnvironmentVariable("Public");
            ENV.UsersDir       = ENV.PublicDir.Replace(@"\Public","");
            ENV.UserData       = Environment.GetEnvironmentVariable("LocalAppData");
            ENV.AppData        = Environment.GetEnvironmentVariable("AppData");
            ENV.Temp           = Environment.GetEnvironmentVariable("Temp");
            ENV.Tmp            = Environment.GetEnvironmentVariable("Tmp");
            ENV.x64os          = Environment.Is64BitOperatingSystem;
            ENV.x64mode        = Environment.Is64BitProcess;
            // ====================================================================================================================
            //   Add System related variables
            // --------------------------------------------------------------------------------------------------------------------
            RegistryKey lclmKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\FileSystem");
            SYS.longpathenabled = (int)lclmKey.GetValue("LongPathsEnabled", "0") == 1;
            // --------------------------------------------------------------------------------------------------------------------
            //   Some variables can return a null value. Validate and fix if necessary
            // --------------------------------------------------------------------------------------------------------------------
            if (!Directory.Exists(ENV.System32Dir)) { ENV.System32Dir = ENV.WinDir + "\\System32"; }
            if (ENV.Temp == null) { ENV.Temp = ENV.SysDrive +"\\Tmp"; }
            if (ENV.Tmp  == null) { ENV.Tmp  = ENV.Temp; }
            // ====================================================================================================================
            //   Add Date & Time related formats and variables.
            // --------------------------------------------------------------------------------------------------------------------
            DTM.DateFormat     = CultureInfo.CurrentUICulture.DateTimeFormat.ShortDatePattern;
            DTM.DateSeparator  = CultureInfo.CurrentUICulture.DateTimeFormat.DateSeparator;
            DTM.TimeFormat     = CultureInfo.CurrentUICulture.DateTimeFormat.LongTimePattern;
            DTM.TimeSeparator  = CultureInfo.CurrentUICulture.DateTimeFormat.TimeSeparator;
            DTM.Format         = DTM.DateFormat + " " + DTM.TimeFormat;
            DTM.Start          = DateTime.Now;
            UpdateDTMData(DTM.Start);
            // ====================================================================================================================
            //   Finally initialize the Keyword Replacement list
            // --------------------------------------------------------------------------------------------------------------------
            InitializeReplacementKeywordList();
            // ====================================================================================================================
            //   InitializeContext is Ready
            // --------------------------------------------------------------------------------------------------------------------
            }

        // ========================================================================================================================
        //   Initialize the application logger context (can provide a default log4net logger!)
        // ------------------------------------------------------------------------------------------------------------------------
        public static void InitializeLog4Net(string logFilename = null, Level logLevel = null)
        {
            if (logLevel == null) { logLevel = Level.Info; }
            // ====================================================================================================================
            //   See whether a 'customer' log4net configuration has been provided. When present use it
            // --------------------------------------------------------------------------------------------------------------------
            ConfigurationSection l4nSection = APP.Configuration.GetSection("log4net");
            if (l4nSection != null)
            {
                XmlDocument l4nXml = new XmlDocument();
                l4nXml.LoadXml(l4nSection.SectionInformation.GetRawXml());
                if (logFilename != null)
                {
                    XmlNodeList nodeList = l4nXml.GetElementsByTagName("file");
                    foreach (XmlElement l4nElement in nodeList)
                    {
                        if (l4nElement.HasAttribute("value")) { l4nElement.SetAttribute("value", ReplaceKeyword(logFilename)); }
                    }
                }
                log4net.Config.XmlConfigurator.Configure(l4nXml.DocumentElement);
                Log4netSetRootLevel(logLevel);
                total.Logger.Debug("Using customer defined log4net configuration");
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Configure the default logger (console and rolling logfile)
            // --------------------------------------------------------------------------------------------------------------------
            else
            {
                if (logFilename != null) { logFilename = ReplaceKeyword(logFilename); }
                else { logFilename = APP.Name + @"_%date{yyyyMMdd}.log"; }
                // ----------------------------------------------------------------------------------------------------------------
                //   Reset the current log config and start a new one
                // ----------------------------------------------------------------------------------------------------------------
                if (total.Logger != null) { total.Logger.Logger.Repository.ResetConfiguration(); }
                Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                string patternLayout = "%date{yyyy-MM-dd HH:mm:ss} - %-5level - %m%n";
                // ----------------------------------------------------------------------------------------------------------------
                //   Create a RollingLogfileAppender
                // ----------------------------------------------------------------------------------------------------------------
                RollingFileAppender rlfa = new RollingFileAppender
                {
                    Name = APP.Name + "-RollingLogfileAppender",
                    File = new PatternString(logFilename).Format(),
                    LockingModel = new FileAppender.MinimalLock(),
                    Layout = new PatternLayout(patternLayout),
                    Threshold = logLevel
                };
                rlfa.ActivateOptions();
                // ----------------------------------------------------------------------------------------------------------------
                //   Create a ManagedColoredConsoleAppender
                // ----------------------------------------------------------------------------------------------------------------
                ManagedColoredConsoleAppender mcca = new ManagedColoredConsoleAppender{ Name = APP.Name + "-ManagedColoredConsoleAppender" };
                mcca.AddMapping(new ManagedColoredConsoleAppender.LevelColors { Level = Level.Fatal, ForeColor = ConsoleColor.Magenta, BackColor = ConsoleColor.Black });
                mcca.AddMapping(new ManagedColoredConsoleAppender.LevelColors { Level = Level.Error, ForeColor = ConsoleColor.Red, BackColor = ConsoleColor.Black });
                mcca.AddMapping(new ManagedColoredConsoleAppender.LevelColors { Level = Level.Warn, ForeColor = ConsoleColor.Yellow, BackColor = ConsoleColor.Black });
                mcca.AddMapping(new ManagedColoredConsoleAppender.LevelColors { Level = Level.Info, ForeColor = ConsoleColor.Gray });
                mcca.AddMapping(new ManagedColoredConsoleAppender.LevelColors { Level = Level.Debug, ForeColor = ConsoleColor.Green, BackColor = ConsoleColor.Gray });
                mcca.Layout = new PatternLayout(patternLayout);
                mcca.Threshold = logLevel;
                mcca.ActivateOptions();
                // ----------------------------------------------------------------------------------------------------------------
                //   Build the hierarchy and start it
                // ----------------------------------------------------------------------------------------------------------------
                hierarchy.Root.AddAppender(rlfa);
                hierarchy.Root.AddAppender(mcca);
                hierarchy.Configured = true;
                // ----------------------------------------------------------------------------------------------------------------
                //   Finaly inform the audience we are working with an embedded log4net configuration
                // ----------------------------------------------------------------------------------------------------------------
                total.Logger.Warn("No customer log4net configuration found! Using embedded configuration settings");
            }
            // ====================================================================================================================
            //   InitializeLog4Net is Ready
            // --------------------------------------------------------------------------------------------------------------------
        }

        // ========================================================================================================================
        //   Set log4net loglevel for the root logger (Default = Info).
        // ------------------------------------------------------------------------------------------------------------------------
        public static void Log4netSetRootLevel(Level loggerLevel = null)
        {
            if (loggerLevel == null) { loggerLevel = Level.Info; }
            Hierarchy hierarchy = LogManager.GetRepository() as Hierarchy;
            hierarchy.Threshold = loggerLevel;
            hierarchy.Root.Level = loggerLevel;
            hierarchy.RaiseConfigurationChanged(EventArgs.Empty);
        }

    }
    // ----------------------------------------------------------------------------------------------------------------------------
    // End of class app (main)
    // ----------------------------------------------------------------------------------------------------------------------------
}
// ================================================================================================================================
//    EOF, Sayonara!
// ================================================================================================================================