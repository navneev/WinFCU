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
        /// returns the first MAC address from where is executed (returns a 00 mac if none is found)
        /// </summary>
        /// <param name="flagUpOnly">if sets returns only the nic on Up status</param>
        /// <returns></returns>
        public static string[] getMacAddresses(Boolean flagUpOnly = false)
        {
//            int i = NetworkInterface.GetAllNetworkInterfaces().Count();
            List<string> macAddresses = new List<string>();
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.GetPhysicalAddress().GetAddressBytes().Length < 6) { continue; }
                if (nic.OperationalStatus == OperationalStatus.Up || !flagUpOnly)
                {
                   macAddresses.Add(BitConverter.ToString(nic.GetPhysicalAddress().GetAddressBytes()));
                }
            }
            if (macAddresses.Count == 0) { macAddresses.Add("00-00-00-00-00-00"); }
            return macAddresses.ToArray();
        }

    }
    // ----------------------------------------------------------------------------------------------------------------------------
    // End of class
    // ----------------------------------------------------------------------------------------------------------------------------
}
// ================================================================================================================================
//    EOF, Sayonara!
// ================================================================================================================================