using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Management.Automation;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using Total.Util;
using Total.CLI;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Util;
using log4net.Repository.Hierarchy;

namespace Total.WinFCU
{
    public partial class fcu
    {
        // ------------------------------------------------------------------------------------------------------------------------
        //  Initialize and start log4net
        // ------------------------------------------------------------------------------------------------------------------------
        public static void InitializeLog4net()
        {
            Level logLevel = (cli.IsPresent("Debug") && !cli.IsNegated("Debug")) ? Level.Debug : Level.Info;
            // --------------------------------------------------------------------------------------------------------------------
            //  See whether a 'customer' log4net configuration has been provided. When present use it
            // --------------------------------------------------------------------------------------------------------------------
            ConfigurationSection l4nSection = total.APP.Configuration.GetSection("log4net");
            if (l4nSection != null)
            {
                log4net.Config.XmlConfigurator.Configure();
                Log4netSetRootLevel(logLevel);
                total.Logger.Debug("Using customer specific log4net configuration");
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Configure the default logger (console and rolling logfile)
            // --------------------------------------------------------------------------------------------------------------------
            else
            {
                // ----------------------------------------------------------------------------------------------------------------
                //  Setup a default console and logfile appender. Use the commandline input if present
                // ----------------------------------------------------------------------------------------------------------------
                string logFilename = @"'Log\WinFCU_'yyyyMMdd'.log'";
                if (cli.IsPresent("logfile")) { logFilename = ReplaceKeyword(cli.GetValue("logfile")); }
                // ----------------------------------------------------------------------------------------------------------------
                //   Reset the current log config and start a new one
                // ----------------------------------------------------------------------------------------------------------------
                if (total.Logger != null) { total.Logger.Logger.Repository.ResetConfiguration(); }
                Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
                PatternLayout patternLayout = new PatternLayout();
                patternLayout.ConversionPattern = "%date{yyyy-MM-dd HH:mm:ss} - %-5level - %m%n";
                patternLayout.ActivateOptions();
                // ----------------------------------------------------------------------------------------------------------------
                //   Create a RollingLogfileAppender
                // ----------------------------------------------------------------------------------------------------------------
                RollingFileAppender rfa = new RollingFileAppender();
                rfa.Name = "WinFCU-RollingLogfileAppender";
                rfa.File = logFilename;
                rfa.StaticLogFileName = false;
                rfa.RollingStyle = RollingFileAppender.RollingMode.Date;
                rfa.Layout = patternLayout;
                rfa.LockingModel = new FileAppender.MinimalLock();
                rfa.Threshold = logLevel;
                rfa.ActivateOptions();
                // ----------------------------------------------------------------------------------------------------------------
                //   Create a ManagedColoredConsoleAppender
                // ----------------------------------------------------------------------------------------------------------------
                ManagedColoredConsoleAppender mcca = new ManagedColoredConsoleAppender();
                mcca.Name = "WinFCU-ManagedColoredConsoleAppender";
                mcca.AddMapping(new ManagedColoredConsoleAppender.LevelColors { Level = Level.Fatal, ForeColor = ConsoleColor.Magenta,  BackColor = ConsoleColor.Black });
                mcca.AddMapping(new ManagedColoredConsoleAppender.LevelColors { Level = Level.Error, ForeColor = ConsoleColor.Red,      BackColor = ConsoleColor.Black });
                mcca.AddMapping(new ManagedColoredConsoleAppender.LevelColors { Level = Level.Warn,  ForeColor = ConsoleColor.Yellow,   BackColor = ConsoleColor.Black });
                mcca.AddMapping(new ManagedColoredConsoleAppender.LevelColors { Level = Level.Info,  ForeColor = ConsoleColor.Gray });
                mcca.AddMapping(new ManagedColoredConsoleAppender.LevelColors { Level = Level.Debug, ForeColor = ConsoleColor.Green,    BackColor = ConsoleColor.Gray });
                mcca.Layout = patternLayout;
                mcca.Threshold = logLevel;
                mcca.ActivateOptions();
                // ----------------------------------------------------------------------------------------------------------------
                //   Build the hierarchy and start it
                // ----------------------------------------------------------------------------------------------------------------
                hierarchy.Root.AddAppender(rfa);
                hierarchy.Root.AddAppender(mcca);
                hierarchy.Configured = true;
                // ----------------------------------------------------------------------------------------------------------------
                //   Finaly inform the audience we are working with an embedded log4net configuration
                // ----------------------------------------------------------------------------------------------------------------
                total.Logger.Warn(" >>>> WinFCU - No customer log4net configuration found! Using embedded configuration settings");
            }
        }

        // ========================================================================================================================
        //   Set log4net loglevel for the root logger (Default = Info).
        // ------------------------------------------------------------------------------------------------------------------------
        public static void Log4netSetRootLevel(Level loggerLevel)
        {
            if (loggerLevel == null) { loggerLevel = Level.Info; }
            Hierarchy hierarchy = LogManager.GetRepository() as Hierarchy;
            hierarchy.Threshold = loggerLevel;
            hierarchy.Root.Level = loggerLevel;
            hierarchy.RaiseConfigurationChanged(EventArgs.Empty);
        }


    }
}
