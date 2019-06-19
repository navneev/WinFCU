using System;
using System.ServiceProcess;
using Total.Util;

namespace Total.WinFCU
{
    static partial class WinFCU
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        private static void Main(string[] args)
        {
            // --------------------------------------------------------------------------------------------------------------------
            //  Initialize the application context (create/fill data structures)
            //  See whether the required application configuration has been provided
            // --------------------------------------------------------------------------------------------------------------------
            total.InitializeContext();

            if (!total.APP.HasConfigFile)
            {
                ConsoleColor fg = Console.ForegroundColor; ConsoleColor bg = Console.BackgroundColor;
                Console.ForegroundColor = ConsoleColor.Red; Console.BackgroundColor = ConsoleColor.Black;
                Console.WriteLine(@" >>>> WinFCU Fatal Error - No application configuration file found!");
                Console.ForegroundColor = fg; Console.BackgroundColor = bg;
                Environment.Exit(1);
            }

            // --------------------------------------------------------------------------------------------------------------------
            //   If started as a service, than run as a service. Else use the interactive path
            // --------------------------------------------------------------------------------------------------------------------
            if (total.PRC.Service)
            {
                ServiceBase[] winfcuService = { new WinFCUService() };
                ServiceBase.Run(winfcuService);
            }
            else
            {
                WinFCU.runInteractive(args);
            }
        }
    }
}
