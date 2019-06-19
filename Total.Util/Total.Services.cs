using System;
using System.Collections.Specialized;
using System.Configuration.Install;
using System.IO;
using System.ServiceProcess;
//using Microsoft.Win32;

namespace Total.Util
{
    public partial class total
    {
        // ========================================================================================================================
        //  Embedded Service Installer. Only servicename is passed, the rest is read from local storage
        // ------------------------------------------------------------------------------------------------------------------------
        private void ServiceInstaller(String ServiceName)
        {
            // --------------------------------------------------------------------------------------------------------------------
            if (ServiceName != SVC.ServiceName) { Logger.Error("Calling service installer with invalid servicename " + ServiceName); }
            // --------------------------------------------------------------------------------------------------------------------
            ServiceProcessInstaller spi   = new ServiceProcessInstaller();
            ServiceInstaller        sin   = new ServiceInstaller();
            InstallContext          ctx   = new InstallContext();
            ListDictionary          state = new ListDictionary();
            StreamWriter            sw    = new StreamWriter(Stream.Null);
            TextWriter              tmp   = Console.Out;
            // --------------------------------------------------------------------------------------------------------------------
            if (SVC.BinaryPath != null && SVC.BinaryPath.Length > 0)
            {
                System.IO.FileInfo fi = new System.IO.FileInfo(SVC.BinaryPath);
                String[] cmdline = { String.Format("/assemblypath={0}", fi.FullName) };
                ctx = new InstallContext("", cmdline);
                ctx.Parameters.Add("", null);
            }
            // --------------------------------------------------------------------------------------------------------------------
            sin.Context = ctx;
            sin.ServiceName = ServiceName;
            sin.DisplayName = SVC.DisplayName;
            sin.Description = SVC.Description;
            sin.StartType   = SVC.StartupType;
            // --------------------------------------------------------------------------------------------------------------------
            spi.Account = SVC.Account;
            if (SVC.Account == ServiceAccount.User)
            {
                spi.Username = SVC.Username;
                spi.Password = SVC.Password;
            }
            sin.Parent = spi;
            Console.SetOut(sw);
            try { sin.Install(state); }
            catch (Exception ex) { Console.SetOut(tmp); Logger.Error(ex.Message); }
            finally { Console.SetOut(tmp); }
            // --------------------------------------------------------------------------------------------------------------------
        }

        // ========================================================================================================================
        //  Embedded Service UnInstaller
        // ------------------------------------------------------------------------------------------------------------------------
        private void ServiceUninstaller(String ServiceName)
        {
            ServiceInstaller sin = new ServiceInstaller();
            InstallContext ctx = new InstallContext("", null);
            StreamWriter sw = new StreamWriter(Stream.Null);
            TextWriter tmp = Console.Out;
            sin.Context = ctx;
            sin.ServiceName = ServiceName;
            Console.SetOut(sw);
            try { sin.Uninstall(null); }
            catch (Exception ex) { Console.SetOut(tmp); Logger.Error(ex.Message); }
            finally { Console.SetOut(tmp); }
            // --------------------------------------------------------------------------------------------------------------------
        }

        // ========================================================================================================================
        //   Install a service using the data provided
        // ------------------------------------------------------------------------------------------------------------------------
        public static bool InstallAsService()
        {
            // --------------------------------------------------------------------------------------------------------------------
            //   Check whether all required info is present. ServiceName is mandatory!
            // --------------------------------------------------------------------------------------------------------------------
            if (SVC.ServiceName.Length == 0)  { Logger.Error("No servicename specified, cannot install the service"); }
            if (SVC.BinaryPath.Length == 0)   { SVC.BinaryPath   = APP.Fullname; }
            if (SVC.Description.Length == 0)  { SVC.Description  = SVC.ServiceName; }
            if (SVC.DisplayName.Length == 0)  { SVC.Description  = SVC.ServiceName; }
            if (SVC.EventLogName.Length == 0) { SVC.EventLogName = "Application"; }
//            if (SVC.StartupType.Length == 0)  { SVC.StartupType  = "Auto"; }
            // --------------------------------------------------------------------------------------------------------------------
            //   
            // --------------------------------------------------------------------------------------------------------------------
            
            
            return true;
        }

    }
}
