using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Total.Util
{
    public partial class total
    {
        // ------------------------------------------------------------------------------------------------------------------------
        //   Set all datetime values in the DTM structure to the specified time. No time specified? then use Now
        // ------------------------------------------------------------------------------------------------------------------------
        public static void UpdateDTMData([Optional]DateTime dt)
        {
            if (dt == null) { dt = DateTime.Now; }
            DTM.Year = String.Format("{0:yyyy}", dt);
            DTM.Month = String.Format("{0:yyyyMM}", dt);
            DTM.Date = String.Format("{0:yyyyMMdd}", dt);
            DTM.Time = String.Format("{0:HHmmss}", dt);
            DTM.DateTime = String.Format("{0:yyyyMMddHHmmss}", dt);
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Find all files in the given path
        // ------------------------------------------------------------------------------------------------------------------------
        public static List<string> FindFileInPath(string file, string path = null, bool recurse = false)
        {
            Queue<string> pathQueue = new Queue<string>();
            List<string> listFiles = new List<string>();
            // --------------------------------------------------------------------------------------------------------------------
            //   Make sure we have a path to scan
            // --------------------------------------------------------------------------------------------------------------------
            if (path == null) { path = Environment.GetEnvironmentVariable("PATH"); }
            while (path.Contains("%"))
            {
                int si = path.IndexOf("%"); int ei = path.IndexOf("%", si + 1) - si - 1;
                string envVar = path.Substring(si + 1, ei);
                string envVal = Environment.GetEnvironmentVariable(envVar);
                path = path.Replace("%" + envVar + "%", envVal);
            }
            // --------------------------------------------------------------------------------------------------------------------
            //  Find all files matching given path and filespec.
            // --------------------------------------------------------------------------------------------------------------------
            foreach (string scanPath in path.Split(';'))
            {
                if (Directory.Exists(scanPath))
                {
                    if (!recurse) { listFiles.AddRange(Directory.GetFiles(scanPath, file)); }
                    else
                    {
                        string pth = scanPath;
                        pathQueue.Enqueue(pth);
                        while (pathQueue.Count > 0)
                        {
                            pth = pathQueue.Dequeue();
                            try { foreach (string subDir in Directory.GetDirectories(pth)) { pathQueue.Enqueue(subDir); } }
                            catch (Exception ex) { Logger.Error(ex.Message); }
                            string[] files = null;
                            try { files = Directory.GetFiles(pth, file); }
                            catch (Exception ex) { Logger.Error(ex.Message); }
                            if (files != null) { listFiles.AddRange(files); }
                        }
                        pathQueue.Clear();
                    }
                }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Return the list of files found
            // --------------------------------------------------------------------------------------------------------------------
            return listFiles;
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Find all folders matching the specified folder spec (can contain wildcards! even ?:\ - all online drives - )
        // ------------------------------------------------------------------------------------------------------------------------
        public static string[] ResolvePath(string basePath, params string[] basePaths)
        {
            char[] wcChars = { '?', '*' };
            Queue<string> pathQueue = new Queue<string>();
            List<string> pathList = new List<string>();
            pathList.AddRange(basePath.Split(',')); pathList.AddRange(basePaths);
            // --------------------------------------------------------------------------------------------------------------------
            //   Do we have a wildcard drive? If so create a queue with all possible (online) drive & folder combinations
            // --------------------------------------------------------------------------------------------------------------------
            foreach (string path in pathList)
            {
                if (!path.StartsWith(@"?:\")) { pathQueue.Enqueue(path); }
                else
                {
                    foreach (DriveInfo Drive in DriveInfo.GetDrives())
                    {
                        if (!Drive.IsReady) { continue; }
                        string newDrive = Drive.RootDirectory.ToString();
                        string newPath = path.Replace(@"?:\", newDrive);
                        pathQueue.Enqueue(newPath);
                    }
                }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   The pathList list now contains every possible drive/folder combination which might still contain wildcards
            //   Resolve all paths matching the input path
            // --------------------------------------------------------------------------------------------------------------------
            pathList.Clear();
            while (pathQueue.Count > 0)
            {
                string thisPath = pathQueue.Dequeue();
                // ----------------------------------------------------------------------------------------------------------------
                //   Check if the path contains wildcards, if not add the path to the final list and go for the next path to scan
                // ----------------------------------------------------------------------------------------------------------------
                if (thisPath.IndexOfAny(wcChars) < 0)
                {
                    if (Directory.Exists(thisPath)) { pathList.Add(Path.GetFullPath(thisPath)); }
                    continue;
                }
                // ----------------------------------------------------------------------------------------------------------------
                //   So the path contains wildcards. Split the path is 3 parts: the part preceeding the wildcard, the part
                //   containing the wildcard, and the remaining part.
                // ----------------------------------------------------------------------------------------------------------------
                int gdIndex1 = thisPath.Substring(0, thisPath.IndexOfAny(wcChars)).LastIndexOf(@"\");
                int gdIndex2 = thisPath.Substring(gdIndex1 + 1).IndexOf(@"\") + 1;
                if (gdIndex2 <= 0) { gdIndex2 = thisPath.Length - gdIndex1; }
                int gdIndex3 = gdIndex1 + gdIndex2 + 1;
                if (gdIndex3 > thisPath.Length) { gdIndex3 = thisPath.Length; }
                string getdirPath    = thisPath.Substring(0, gdIndex1 + 1);
                string searchPattern = thisPath.Substring(gdIndex1 + 1, gdIndex2 - 1);
                string remainingPath = thisPath.Substring(gdIndex3);
                // ----------------------------------------------------------------------------------------------------------------
                //   If the base directory exists, scan it using the searchPattern and add all found subdirs to the queue
                // ----------------------------------------------------------------------------------------------------------------
                if (Directory.Exists(getdirPath))
                {
                    try
                    {
                        foreach (string subDir in Directory.GetDirectories(getdirPath, searchPattern, SearchOption.TopDirectoryOnly))
                        {
                            string newPath = Path.Combine(subDir, remainingPath);
                            pathQueue.Enqueue(newPath);
                        }
                    }
                    catch (Exception ex) { Logger.Error(ex.Message); }
                }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   The created list might contain duplicates. Use the Distinct method to remove them
            // --------------------------------------------------------------------------------------------------------------------
            pathQueue.Clear();
            return pathList.Distinct().ToArray();
        }
    }

    public partial class SaveNativeMethods
    {
        // ------------------------------------------------------------------------------------------------------------------------
        //   Return int array with the FreeBytesAvailable, TotalNumberOfBytes and TotalNumberOfFreeByte of a drive/UNC path
        // ------------------------------------------------------------------------------------------------------------------------
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetDiskFreeSpaceEx(string dirName, out ulong freeBytesAvailable, out ulong totalNumberOfBytes, out ulong totalNumberOfFreeBytes);
        // ------------------------------------------------------------------------------------------------------------------------
        public static long[] GetFreeDiskSpace(string path)
        {
            long[] result = new long[3];
            bool sts = GetDiskFreeSpaceEx(Path.GetPathRoot(path), out ulong freeBytesAvailable, out ulong totalNumberOfBytes, out ulong totalNumberOfFreeBytes);
            if (!sts) { throw new System.ComponentModel.Win32Exception(); }
            result[0] = Convert.ToInt64(freeBytesAvailable);
            result[1] = Convert.ToInt64(totalNumberOfBytes);
            result[2] = Convert.ToInt64(totalNumberOfFreeBytes);
            return result;
        }
    }
    // ----------------------------------------------------------------------------------------------------------------------------
    // End of class
    // ----------------------------------------------------------------------------------------------------------------------------
}
// ================================================================================================================================
//    EOF, Sayonara!
// ================================================================================================================================