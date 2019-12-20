using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Total.Util
{
    public partial class total
    {
        /// <summary>
        /// returns the first MAC address from where is executed 
        /// </summary>
        /// <param name="flagUpOnly">if sets returns only the nic on Up status</param>
        /// <returns></returns>
        public static string[] getMacAddresses(Boolean flagUpOnly = false)
        {
            int i = NetworkInterface.GetAllNetworkInterfaces().Count();
            string[] macAddresses = new string[i];
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up || !flagUpOnly)
                {
                    macAddresses[--i] = nic.GetPhysicalAddress().ToString();
                }
            }
            return macAddresses;
        }

    }
    // ----------------------------------------------------------------------------------------------------------------------------
    // End of class
    // ----------------------------------------------------------------------------------------------------------------------------
}
// ================================================================================================================================
//    EOF, Sayonara!
// ================================================================================================================================