using System.ComponentModel;
using System.ServiceProcess;

namespace Total.WinFCU
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;
        private ServiceProcessInstaller WinFCUProcessInstaller;
        private ServiceInstaller WinFCUService;

        public const string SvcServiceName = "WinFCUService";
        public const string SvcDisplayName = "Windows Filesystem Cleanup Utility";
        public const string SvcDesription  = "Rule base utility which keeps the filesystem clean by archiving/deleting/moving unwanted/unneeded files from the filesystem";

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.WinFCUProcessInstaller = new ServiceProcessInstaller();
            this.WinFCUService          = new ServiceInstaller();
            // 
            // WinFCUProcessInstaller
            // 
            this.WinFCUProcessInstaller.Account  = ServiceAccount.LocalSystem;
            this.WinFCUProcessInstaller.Password = null;
            this.WinFCUProcessInstaller.Username = null;
            // 
            // WinFCUService
            // 
            this.WinFCUService.ServiceName      = SvcServiceName;
            this.WinFCUService.DisplayName      = SvcDisplayName;
            this.WinFCUService.Description      = SvcDesription;
            this.WinFCUService.DelayedAutoStart = true;
            this.WinFCUService.StartType        = ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            //
            this.Installers.Add(WinFCUProcessInstaller);
            this.Installers.Add(WinFCUService);
        }

        #endregion
    }
}