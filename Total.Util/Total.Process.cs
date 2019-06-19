using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace Total.Util
{
    public partial class total
    {
        // ========================================================================================================================
        //   Get all process info of the one who give birth to me.
        // ------------------------------------------------------------------------------------------------------------------------
        public static Process GetParentProccess([Optional] int pid)
        {
            if (pid == 0) { pid = Process.GetCurrentProcess().Id; }
            var query = string.Format("SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {0}", pid);
            var search = new ManagementObjectSearcher("root\\CIMV2", query);
            var results = search.Get().GetEnumerator();
            if (!results.MoveNext()) throw new Exception("Huh? Where is my parent gone......");
            var queryObj = results.Current;
            uint parentId = (uint)queryObj["ParentProcessId"];
            return (Process.GetProcessById((int)parentId));
        }

        // ========================================================================================================================
        //   Get process logon info type name & number. A for windows non-existing number/name can be returned!
        //   A service or batchjob is running under the System account will always return 0 as logontype. In such a case, this
        //   function will determine batch or service and will return 14 (LS-Batch) or 15 (LS-Service).
        // ------------------------------------------------------------------------------------------------------------------------
        public static int GetProcessLogonType([Optional] int pid)
        {
            int lgiType = 0;
            if (pid == 0) { pid = Process.GetCurrentProcess().Id; }
            string processHandle = String.Format("Win32_Process.Handle=\"{0}\"", pid);
            // --------------------------------------------------------------------------------------------------------------------
            //  First get the LogonID of the requested process
            // --------------------------------------------------------------------------------------------------------------------
            SelectQuery sq = new SelectQuery("Win32_SessionProcess");
            ManagementObjectSearcher mos = new ManagementObjectSearcher(sq);
            foreach (ManagementObject mo in mos.Get())
            {
                if (!mo["Dependent"].ToString().EndsWith(processHandle, StringComparison.OrdinalIgnoreCase)) { continue; }
                PRC.LogonID = mo["Antecedent"].ToString().Split('=').Last().Trim('"');
                break;
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Use the found LogonID to access the LogonSession, thgis object holds the LogonType we are looking for
            // --------------------------------------------------------------------------------------------------------------------
            sq = new SelectQuery("Win32_LogonSession", ("LogonId=" + PRC.LogonID));
            mos = new ManagementObjectSearcher(sq);
            foreach (ManagementObject mo in mos.Get()) { lgiType = Convert.ToInt32(mo["LogonType"]); }
            // --------------------------------------------------------------------------------------------------------------------
            //  LogonType will return 0 (zero) when a service or batchjob is running under the System account.
            //  Lets see whether we can determine the 'correct' logon type. Turns out that for a service the parent process is
            //  "services" while a batchjob has svchost as parent
            // --------------------------------------------------------------------------------------------------------------------
            if (lgiType == 0)
            {
                Process pprc = GetParentProccess(pid);
                if (pprc.ProcessName == "services") { lgiType = 15; }
                if (pprc.ProcessName == "svchost")  { lgiType = 14; }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Also return (via a predefined struct entry - PRC) the process type in clear text
            // --------------------------------------------------------------------------------------------------------------------
            PRC.TypeName = (new string[] {"Local System Service", "undef" , "Interactive", "Network", "Batch", "Service", "Proxy",
                                            "Unlock", "NetworkClearText", "NewCredentials","RemoteInteractive", "CachedInteractive",
                                            "CachedUnlock", "undef", "LS-Batch", "LS-Service"})[lgiType];
            // --------------------------------------------------------------------------------------------------------------------
            //  Add both value to the ArrayList and return it to the caller
            // --------------------------------------------------------------------------------------------------------------------
            return lgiType;
        }

        // ========================================================================================================================
        //   Get a list of processes locking a specific file
        // ------------------------------------------------------------------------------------------------------------------------
        public static ArrayList GetFileProcesses(string FileToCheck)
        {
            var processArray = new ArrayList();
            foreach (Process checkProcess in Process.GetProcesses())
            {
                try
                {
                    if (checkProcess.HasExited) { continue; }
                    try
                    {
                        foreach (ProcessModule checkModule in checkProcess.Modules)
                        {
                            if (String.Equals(checkModule.FileName, FileToCheck, StringComparison.OrdinalIgnoreCase))
                            {
                                processArray.Add(checkProcess);
                                break;
                            }
                        }
                    }
                    catch { }
                }
                catch { }
            }
            return processArray;
        }

    }
    // ----------------------------------------------------------------------------------------------------------------------------
    // End of class app (process)
    // ----------------------------------------------------------------------------------------------------------------------------
}
// ================================================================================================================================
//    EOF, Sayonara!
// ================================================================================================================================