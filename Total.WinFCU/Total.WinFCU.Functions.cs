using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using System.Xml;
using Total.Util;
using Total.CLI;
using log4net.Appender;
using log4net.Core;

namespace Total.WinFCU
{
    public partial class fcu
    {
        public static Boolean parallelSchedules = false;
        public static EventSchedule evtSch = new EventSchedule();
        private static scanAttributes defAttributes = new scanAttributes();
        public static Dictionary<char, string> checkType = new Dictionary<char, string>() { { 'm', "Modified" }, { 'c', "Created" } };
        // private static string WildcardToRegex(string pattern) { return "^" + pattern.Replace(@".", @"\.").Replace(@"*", @".*").Replace(@"?", @".") + "$"; }
        private static string[] GetAllFilesFromFolder(string root, SearchOption searchSubfolders, string filename = "*.*")
        {
            Queue<string> folders = new Queue<string>();
            List<string> files = new List<string>();
            // Add the 'root' folder to the queue. Subfolders will be added later (if requested!)
            folders.Enqueue(root);
            while (folders.Count != 0)
            {
                // Pull a folder from the queue and process the files found in there
                string currentFolder = folders.Dequeue();
                try
                {
                    string[] filesInCurrent = Directory.GetFiles(currentFolder, filename, SearchOption.TopDirectoryOnly);
                    files.AddRange(filesInCurrent);
                }
                catch (PathTooLongException exPTL) { total.Logger.Debug(exPTL.Message + " - " + currentFolder); continue; }
                catch (DirectoryNotFoundException exDNF) { total.Logger.Debug(exDNF.Message); continue; }
                catch (UnauthorizedAccessException exUAE) { total.Logger.Debug(exUAE.Message); continue; }
                catch (ArgumentException exAE) { total.Logger.Debug(exAE.Message.TrimEnd('.') + " \"" + currentFolder + "\""); continue; }
                catch (IOException exIO) { total.Logger.Debug(exIO.Message); continue; }
                // Scan for subfolders? Add them to the queue and process them
                if (searchSubfolders == SearchOption.TopDirectoryOnly) { continue; }
                try
                {
                    foreach (string folder in Directory.GetDirectories(currentFolder)) { folders.Enqueue(folder); }
                }
                catch { continue; }
            }
            return files.ToArray();
        }
        // private static void ShowFolderAttributes(scanAttributes fa)
        // {
        //     total.Logger.Debug("Attributes for the current FolderSet:");
        //     total.Logger.Debug("-------------------------------------");
        //     total.Logger.Debug(" - processHiddenFiles: " + fa.processHiddenFiles);
        //     total.Logger.Debug(" - deleteEmptyFolders: " + fa.deleteEmptyFolders);
        //     total.Logger.Debug(" - forceOverWrite:     " + fa.forceOverWrite);
        //     total.Logger.Debug(" - recursiveScan:      " + fa.recursiveScan);
        //     total.Logger.Debug(" - excludePath:        " + fa.excludeFromScan);
        //     total.Logger.Debug(" - scheduleName:       " + fa.scheduleName);
        //     total.Logger.Debug(" - systemName:         " + fa.systemName);
        // }
        private static void ExecutePrePostAction(string Action, string Type)
        {
            string Message = String.Format("Performing {0}: {1}", Type, Action);
            if (total.APP.Dryrun) { total.Logger.Info(" [DRYRUN] - " + Message); }
            else
            {
                total.Logger.Info(Message);
                try
                {
                    List<string> psResults = InvokePowerShell(Action);
                    foreach (string result in psResults) { if (total.Logger.IsDebugEnabled) { total.Logger.Debug(String.Format("{0} result: {1}", Type, result)); } }
                }
                catch (Exception ex) { total.Logger.Warn("FolderSet " + Type + " failed due to: " + ex.Message); }
            }
        }
        private static void ValidateFileCheck(char checkType, string section)
        {
            if ((checkType != 'm') && (checkType != 'c'))
            {
                total.Logger.Error("Invalid check type detected in \"" + section + "\", only 'Modified' or 'Created' are allowed. Terminating process");
                Environment.Exit(1);
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Reset all the total counter values
        // ------------------------------------------------------------------------------------------------------------------------
        public static void ZeroTotalCounters()
        {
            total_bytesZipped = 0;
            total_CompressRatio = 0;
            total_bytesCompacted = 0;
            total_CompactionRatio = 0;
            total_bytesArchived = 0;
            total_bytesDeleted = 0;
            total_bytesMoved = 0;
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Set the required userkeyword values
        // ------------------------------------------------------------------------------------------------------------------------
        public static void SetKeywordValues(FileInfo fileInfo)
        {
            // --------------------------------------------------------------------------------------------------------------------
            //   Load the current content of the app sortedlist.
            // --------------------------------------------------------------------------------------------------------------------
            total.DTM.Year = String.Format("{0:yyyy}", DateTime.Now);
            total.DTM.Month = String.Format("{0:yyyyMM}", DateTime.Now);
            total.DTM.Date = String.Format("{0:yyyyMMdd}", DateTime.Now);
            total.DTM.Time = String.Format("{0:HHmmss}", DateTime.Now);
            total.DTM.DateTime = String.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
            // --------------------------------------------------------------------------------------------------------------------
            //   Now update the fileinfo structure
            // --------------------------------------------------------------------------------------------------------------------
            INF.fileName     = fileInfo.FullName;
            INF.fileExt      = fileInfo.Extension;
            INF.filePath     = fileInfo.DirectoryName;
            INF.scheduleName = "#ALL#";
            INF.fileBaseName = Path.GetFileNameWithoutExtension(fileInfo.Name);
            INF.accessDate   = fileInfo.LastAccessTime.ToString("yyyyMMdd");
            INF.accessMonth  = INF.accessDate.Substring(0, 6);
            INF.accessYear   = INF.accessDate.Substring(0, 4);
            INF.createDate   = fileInfo.CreationTime.ToString("yyyyMMdd");
            INF.createMonth  = INF.createDate.Substring(0, 6);
            INF.createYear   = INF.createDate.Substring(0, 4);
            INF.modifyDate   = fileInfo.LastWriteTime.ToString("yyyyMMdd");
            INF.modifyMonth  = INF.modifyDate.Substring(0, 6);
            INF.modifyYear   = INF.modifyDate.Substring(0, 4);
            // --------------------------------------------------------------------------------------------------------------------
            //   Breakdown the filepath info separate folders keywords (max 10)
            // --------------------------------------------------------------------------------------------------------------------
            string[] subDirs = fileInfo.DirectoryName.Split('\\');
            for (int i = 0; i < 10; i++) { INF.fileRdir[i] = INF.fileSdir[i] = ""; }
            INF.fileDirCount = (subDirs.Length > 9) ? 9 : subDirs.Length - 1;
            for (int i = 0; i <= INF.fileDirCount; i++) { INF.fileRdir[i] = INF.fileSdir[INF.fileDirCount - i] = subDirs[i]; }
            // --------------------------------------------------------------------------------------------------------------------
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Load process, environment, configuration and other details into local storage. Some parts are main config only, these
        //   settings are located in the <startup> node. Other parts can be defined in multiple locations (config files), these
        //   settings are located in the <applicationSettings> node. Logging is now done via the <log4net> settings
        //   Beware of Inheritable and Non-Inheritable attributes!
        // ------------------------------------------------------------------------------------------------------------------------
        public static void LoadConfiguration(string fcuConfigFile)
        {
            string xmlTransNode = "/*[translate(local-name(),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='{0}']";
            // --------------------------------------------------------------------------------------------------------------------
            //   Check the existance of the config file and if it does not exist - Quit!
            // --------------------------------------------------------------------------------------------------------------------
            fcu.runConfig = fcu.ReplaceKeyword(fcuConfigFile);
            if (!File.Exists(fcu.runConfig)) { total.Logger.Error("Config file \"" + fcu.runConfig + "\" not found, terminating now..."); Environment.Exit(1); }
            // --------------------------------------------------------------------------------------------------------------------
            //  Read the main config file content and return it as an XML document
            // --------------------------------------------------------------------------------------------------------------------
            fcu.fswTargets.Clear();
            fcu.fswTargets.Add(fcu.runConfig);
            total.Logger.Debug("Loading primary config file: " + fcu.runConfig);
            fcu.fcuConfig = LoadXmlDocument(fcu.runConfig);
            // --------------------------------------------------------------------------------------------------------------------
            //  The config file is loaded, lets process the startup content
            //   - runOptions
            //   - defaults
            //   - includeFiles
            // --------------------------------------------------------------------------------------------------------------------
            //   XML query for RunOptions, get them all. Re-apply the commandline settings afterwards
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading Run options");
            total.APP.Debug = false; total.APP.Dryrun = false;
            string runQuery = String.Format(xmlTransNode, "configuration") +
                              String.Format(xmlTransNode, "applicationsettings") +
                              String.Format(xmlTransNode, "runoptions");
            try
            {
                foreach (XmlAttribute attribute in fcu.fcuConfig.SelectSingleNode(runQuery).Attributes)
                {
                    string defValue = attribute.Name.ToLower();
                    switch (defValue)
                    {
                        case "debug":             total.APP.Debug   = attribute.Value.ToLower() == "true"; break;
                        case "dryrun":            total.APP.Dryrun  = attribute.Value.ToLower() == "true"; break;
                        case "parallelschedules": parallelSchedules = attribute.Value.ToLower() == "true"; break;
                        default: total.Logger.Error("Invalid RunOption setting " + defValue + " detected, terminating process"); Environment.Exit(1); break;
                    }
                }
                if (cli.IsPresent("Dryrun")) { total.APP.Dryrun = Convert.ToBoolean(cli.GetValue("Dryrun")); }
                if (cli.IsPresent("Debug")) { total.APP.Debug = Convert.ToBoolean(cli.GetValue("Debug")); }
                if (total.APP.Debug) { total.Log4netSetRootLevel(Level.Debug); } else { total.Log4netSetRootLevel(Level.Info); }
            }
            catch (Exception ex) { total.Logger.Fatal("Error processing Defaults settings.\n", ex); Environment.Exit(1); }
            // --------------------------------------------------------------------------------------------------------------------
            //   XML query for Defaults, get them all (which are inheritable attributes!).
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading Defaults section");
            // Non-inheritable
            defAttributes.actionName         = "";
            defAttributes.actionTarget       = "";
            defAttributes.fileName           = "";
            // Inheritable
            defAttributes.excludeFromScan    = new Regex("#NONE#", RegexOptions.IgnoreCase);
            defAttributes.deleteEmptyFolders = false;
            defAttributes.fileAge            = "10";
            defAttributes.checkType          = 'm';
            defAttributes.forceOverWrite     = false;
            defAttributes.processHiddenFiles = false;
            defAttributes.recursiveScan      = false;
            defAttributes.archivePath        = "None";
            defAttributes.scheduleName       = "#ALL#";
            defAttributes.systemName         = new Regex(".*", RegexOptions.IgnoreCase);

            string defQuery                  = String.Format(xmlTransNode, "configuration") +
                                               String.Format(xmlTransNode, "applicationsettings") +
                                               String.Format(xmlTransNode, "defaults");
            try
            {
                foreach (XmlAttribute attribute in fcu.fcuConfig.SelectSingleNode(defQuery).Attributes)
                {
                    string defValue = attribute.Name;
                    switch (defValue.ToLower())
                    {
                        case "deleteemptyfolders": defAttributes.deleteEmptyFolders = Convert.ToBoolean(attribute.Value); break;
                        case "age":                defAttributes.fileAge            = attribute.Value; break;
                        case "check":              defAttributes.checkType          = attribute.Value.ToLower()[0]; break;
                        case "forceoverwrite":     defAttributes.forceOverWrite     = Convert.ToBoolean(attribute.Value); break;
                        case "processhiddenfiles": defAttributes.processHiddenFiles = Convert.ToBoolean(attribute.Value); break;
                        case "recursive":          defAttributes.recursiveScan      = Convert.ToBoolean(attribute.Value); break;
                        case "archivepath":        defAttributes.archivePath        = attribute.Value; break;
                        case "schedule":           defAttributes.scheduleName       = attribute.Value; break;
                        case "system":             defAttributes.systemName         = new Regex(attribute.Value, RegexOptions.IgnoreCase); break;
                        case "exclude":            defAttributes.excludeFromScan    = new Regex(attribute.Value, RegexOptions.IgnoreCase); break;
                        default: total.Logger.Error("Invalid Defaults setting \"" + defValue + "\" detected, terminating process"); Environment.Exit(1); break;
                    }
                }
            }
            catch (Exception ex) { total.Logger.Fatal("Error processing Defaults settings.\n", ex); Environment.Exit(1); }
            // --------------------------------------------------------------------------------------------------------------------
            //   File age check type can only be 'c' (Created) or 'm' (Modified) - Quit if something else is specified
            // --------------------------------------------------------------------------------------------------------------------
            ValidateFileCheck(defAttributes.checkType, "Defaults");
            // --------------------------------------------------------------------------------------------------------------------
            //   Logging is performed via log4net. So ask log4net for its name!
            // --------------------------------------------------------------------------------------------------------------------
            foreach (IAppender l4nAppender in (total.Logger.Logger.Repository.GetAppenders()))
            {
                Type t = l4nAppender.GetType();
                if (!t.Equals(typeof(FileAppender)) & !t.Equals(typeof(RollingFileAppender))) { continue; }
                fcu.runLogFile = ((FileAppender)l4nAppender).File;
                break;
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Time to include the extra config files. Additional config files can define;
            //    - extra keywords
            //    - extra restricted paths
            //    - extra schedules
            //    - extra folders and files
            //   Create one big xml document containing all that information
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading Include file(s)");
            string incFileLoading = "";
            string incQuery = String.Format(xmlTransNode, "configuration") +
                              String.Format(xmlTransNode, "applicationsettings") +
                              String.Format(xmlTransNode, "includefiles") +
                              String.Format(xmlTransNode, "add");
            try
            {
                fcu.includeFiles.Clear();
                foreach (XmlElement incNode in fcu.fcuConfig.SelectNodes(incQuery))
                {
                    string includePath = ""; string includeFile = "";
                    // Validate the specified attribute(s)
                    foreach (XmlAttribute incAttribute in incNode.Attributes)
                    {
                        switch (incAttribute.Name.ToLower())
                        {
                            case "path": includePath = incAttribute.Value; break;
                            default: total.Logger.Error("Invalid attribute \"" + incAttribute.Name + "\" found in includeFiles"); break;
                        }
                    }
                    // Validate the validity of the file (does it/do they exist)
                    if (includePath.Length == 0) { total.Logger.Warn("No includeFiles path specified."); return; }
                    includePath = fcu.ReplaceKeyword(includePath);
                    if (!Path.IsPathRooted(includePath)) { includePath = Path.Combine(total.APP.Path, includePath); }
                    includeFile = Path.GetFileName(includePath);
                    if (includeFile.Length == 0) { includeFile = "*.config"; }
                    includePath = Path.GetDirectoryName(includePath);
                    DirectoryInfo incDirInfo = new DirectoryInfo(includePath);
                    if (!incDirInfo.Exists) { total.Logger.Warn("Specified includeFiles path does not exist."); }
                    else
                    // Process all files found
                    {
                        fcu.fswTargets.Add(Path.Combine(includePath, includeFile));
                        FileInfo[] incFileInfo = incDirInfo.GetFiles(includeFile);
                        foreach (FileInfo incFile in incFileInfo)
                        {
                            incFileLoading = incFile.FullName;
                            total.Logger.Debug("Processing include file " + incFileLoading);
                            XmlDocument incConfig = LoadXmlDocument(incFileLoading);
                            // ----------------------------------------------------------------------------------------------------
                            //  The config file is loaded, merge its applicationSettings content with that of the already loaded
                            //  config files
                            // ----------------------------------------------------------------------------------------------------
                            string appQuery = String.Format(xmlTransNode, "configuration") +
                                              String.Format(xmlTransNode, "applicationsettings");
                            XmlNodeList incAppList = incConfig.SelectNodes(appQuery);
                            // Is this include file meant for this system??
                            string incSystem = "";
                            try   { foreach (XmlNode incApp in incAppList) { incSystem = incApp.Attributes.GetNamedItem("system").Value; } }
                            catch (NullReferenceException) { incSystem = ""; }
                            if ((String.IsNullOrEmpty(incSystem)) || (Regex.Match(total.ENV.Server, incSystem, RegexOptions.IgnoreCase).Success))
                            {
                                fcu.includeFiles.Add(incFileLoading);
                            }
                            else
                            {
                                total.Logger.Debug("Skipping include file \"" + incFileLoading + "\"");
                                fcu.includeFiles.Add("*" + incFileLoading);
                                continue;
                            }
                            // Merge the found includefile content with the current set
                            foreach (XmlNode node in incConfig.DocumentElement.ChildNodes)
                            {
                                XmlNode imported = fcu.fcuConfig.ImportNode(node, true);
                                fcu.fcuConfig.DocumentElement.AppendChild(imported);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { total.Logger.Fatal("Fatal error processing include file \"" + incFileLoading + "\"", ex); Environment.Exit(1); }
            // --------------------------------------------------------------------------------------------------------------------
            //  All config files are loaded, lets process the contents
            // --------------------------------------------------------------------------------------------------------------------
            // string foldersQuery = String.Format(xmlTransNode, "configuration") +
            //                       String.Format(xmlTransNode, "applicationsettings") +
            //                       String.Format(xmlTransNode, "folders");
            // XmlNodeList fcuNodeList = fcu.fcuConfig.SelectNodes(foldersQuery);
            // --------------------------------------------------------------------------------------------------------------------
            //   Restricted paths! - Some paths you just do not want to be cleaned. Paths like C:\, C:\Windows, C:\Users, etc.
            //   The restrictedPath is preloaded with some path WinFCU will not clean by default.
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading Restricted Path definitions");
            string rpQuery = String.Format(xmlTransNode, "configuration") +
                             String.Format(xmlTransNode, "applicationsettings") +
                             String.Format(xmlTransNode, "restrictedpaths") +
                             String.Format(xmlTransNode, "add");
            // Preload the restricted path list
            restrictedPaths.Clear();
            restrictedPaths.Add(total.ENV.SysDrive + "\\");
            restrictedPaths.Add(total.ENV.WinDir);
            restrictedPaths.Add(total.ENV.SysRoot);
            restrictedPaths.Add(total.ENV.ProgramFiles);
            restrictedPaths.Add(total.ENV.ProgramFiles86);
            restrictedPaths.Add(total.ENV.ProgramData);
            restrictedPaths.Add(total.ENV.UsersDir);
            // Now add the ones from the config file
            try
            {
                foreach (XmlElement rpNode in fcu.fcuConfig.SelectNodes(rpQuery))
                {
                    string rpValue = "";
                    foreach (XmlAttribute rpAttribute in rpNode.Attributes)
                    {
                        switch (rpAttribute.Name.ToLower())
                        {
                            case "path": rpValue = rpAttribute.Value; break;
                            default: total.Logger.Error("Invalid attribute \"" + rpAttribute.Name + "\" found in " + rpNode.OuterXml); break;
                        }
                        if (rpValue.Length > 0) { restrictedPaths.Add(rpValue); }
                        total.Logger.Debug("Added " + rpValue + " to the restricted path list");
                    }
                }
            }
            catch (Exception ex) { total.Logger.Fatal("Error processing WinFCU Restricted Path entries.\n", ex); Environment.Exit(1); }
            // --------------------------------------------------------------------------------------------------------------------
            //   User defined keywords can be specified in the "keyWords" node, see if it is there and if so process it.
            //   Each user defined keyword is eiter a PowerShell variable of PowerShell scriptblock!
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading User Keyword definitions");
            string kwQuery = String.Format(xmlTransNode, "configuration") +
                             String.Format(xmlTransNode, "applicationsettings") +
                             String.Format(xmlTransNode, "keywords") +
                             String.Format(xmlTransNode, "add");
            try
            {
                // total.usrKeywords.Clear();
                foreach (XmlElement kwNode in fcu.fcuConfig.SelectNodes(kwQuery))
                {
                    string kwKey = ""; string kwValue = ""; string kwDescription = "N/A";
                    foreach (XmlAttribute kwAttribute in kwNode.Attributes)
                    {
                        switch (kwAttribute.Name.ToLower())
                        {
                            case "key":         kwKey         = kwAttribute.Value; break;
                            case "value":       kwValue       = fcu.ReplaceKeyword(kwAttribute.Value); break;
                            case "description": kwDescription = kwAttribute.Value; break;
                            default: total.Logger.Error("Invalid attribute \"" + kwAttribute.Name + "\" found in " + kwNode.OuterXml); break;
                        }
                    }
                    if (kwKey.Length == 0)   { total.Logger.Warn("No key attribute in line: " + kwNode.OuterXml); continue; }
                    if (kwValue.Length == 0) { total.Logger.Warn("No scriptblock attribute in line: " + kwNode.OuterXml); continue; }
                    total.Logger.Debug("Verifying user keyword: " + kwKey + " => " + kwValue);
                    List<string> psResults = InvokePowerShell(kwValue);
                    foreach (string result in psResults) { total.AddReplacementKeyword(kwKey, result, "USER: " + kwDescription); }
                }
            }
            catch (Exception ex) { total.Logger.Fatal("Error processing WinFCU User Keywords.\n", ex); Environment.Exit(1); }
            // --------------------------------------------------------------------------------------------------------------------
            //   OBSOLETE - Check for security settings. Credential replacement has already been done, so just get the encrypted string
            // --------------------------------------------------------------------------------------------------------------------
/*          total.Logger.Debug("Loading Security settings");
            string secQuery = string.Format(xmlTransNode, "configuration") +
                              String.Format(xmlTransNode, "applicationsettings") +
                              String.Format(xmlTransNode, "appsettings") +
                              String.Format(xmlTransNode, "security");
            try
            {
                XmlNode secNode = fcu.fcuConfig.SelectSingleNode(secQuery);
                if ((secNode != null) && (secNode.Attributes[0].Value.Length > 0)) { fcu.secCredentials = secNode.Attributes[0].Value; }
            }
            catch (Exception ex) { total.Logger.Fatal("Error processing Security settings.\n", ex); Environment.Exit(1); }
*/
            // --------------------------------------------------------------------------------------------------------------------
            //   If schedules are defined, create a combined schedule object. Additional requests are appended
            //   Build the XML query for the scheduler settings, and get them all. Clear existing schedules before adding new
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading Schedule definitions");
            string schQuery = String.Format(xmlTransNode, "configuration") +
                              String.Format(xmlTransNode, "applicationsettings") +
                              String.Format(xmlTransNode, "schedules") +
                              String.Format(xmlTransNode, "add");
            bool runHasSchedule = false;
            try
            {
                evtSch.ClearSchedules();
                foreach (XmlElement schNode in fcu.fcuConfig.SelectNodes(schQuery))
                {
                    string schName     = "";
                    string schDays     = "";
                    string schStart    = "";
                    bool   schStrict   = true;
                    string schEnd      = "";
                    string schInterval = "";
                    string schSystem   = "";
                    foreach (XmlAttribute schAttribute in schNode.Attributes)
                    {
                        switch (schAttribute.Name.ToLower())
                        {
                            case "name":      schName     = schAttribute.Value; break;
                            case "day":       schDays     = schAttribute.Value; break;
                            case "start":     schStart    = schAttribute.Value; break;
                            case "strict":    schStrict   = (schAttribute.Value == "true"); break;
                            case "end":       schEnd      = schAttribute.Value; break;
                            case "interval":  schInterval = schAttribute.Value; break;
                            case "system":    schSystem   = schAttribute.Value; break;
                            default: total.Logger.Error("Invalid schedule attribute \"" + schAttribute.Name + "\" found in " + schNode.OuterXml); break;
                        }
                    }
                    if ((String.IsNullOrEmpty(schSystem)) || (Regex.Match(total.ENV.Server, schSystem, RegexOptions.IgnoreCase).Success)) { runHasSchedule = evtSch.AddSchedule(schName, schDays, schStart, schEnd, schInterval, schSystem, schStrict); }
                }
                if (runHasSchedule) { evtSch.CreateRunList(); }
            }
            catch (Exception ex) { total.Logger.Fatal("Error processing Schedule settings.\n", ex); Environment.Exit(1); }
            // --------------------------------------------------------------------------------------------------------------------
            //   Finally get the list of folders and files to scan.....
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading Folders section");
            string folderQuery = String.Format(xmlTransNode, "configuration") +
                                 String.Format(xmlTransNode, "applicationsettings") +
                                 String.Format(xmlTransNode, "folders");
            try { fcu.fcuFolders = fcu.fcuConfig.SelectNodes(folderQuery); }
            catch (Exception ex) { total.Logger.Fatal("Error processing Folders section.\n", ex); Environment.Exit(1); }
            // --------------------------------------------------------------------------------------------------------------------
            //   Lets put all info into the prcInfo string so it will be written to the logfile when the config is (re)loaded
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug(GetStatus());
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Show the security credentials (if they have been provided!)
        // ------------------------------------------------------------------------------------------------------------------------
/*      public static void ShowCredentials()
        {
            if (fcu.secCredentials.Length == 0) { Console.WriteLine("No Credentials found"); }
            string[] scParts = total.DecryptCredentials(fcu.secCredentials);
            Console.WriteLine("WinFCU Credentials found for: " + scParts[0]);
        }
*/

        // ------------------------------------------------------------------------------------------------------------------------
        //   Start cleaning the file system based upon the content of fci.fcuFolders
        // ------------------------------------------------------------------------------------------------------------------------
        public static void CleanFileSystem(string scheduleName = "#ALL#")
        {
            INF.scheduleName = scheduleName;
            int vLen = 16 + total.APP.Version.Length; int cLen = (80 - vLen) / 2;
            string Header = String.Format("{0} W I N F C U - {1} {0}", new String('=', cLen), total.APP.Version);
            if (Header.Length < 80) { Header += "="; }
            string startMessage = "Starting Filesystem Cleanup for schedule " + INF.scheduleName;
            string dash80 = new string('-', 80);
            // --------------------------------------------------------------------------------------------------------------------
            //   Display log header and start message
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Info(Header);
            if (EventLogInitialized) { evtLog.WriteEntry(startMessage); }
            total.Logger.Info(startMessage);
            if (!total.SYS.longpathenabled) { total.Logger.Warn("System does not support long file paths (LongPathsEnabled)"); }
            total.Logger.Info(dash80);
            // --------------------------------------------------------------------------------------------------------------------
            // Clear counters before commencing
            // --------------------------------------------------------------------------------------------------------------------
            total_bytesCompacted = total_bytesDeleted = total_bytesMoved = total_bytesArchived = 0;
            // --------------------------------------------------------------------------------------------------------------------
            foreach (XmlElement folderSet in fcu.fcuFolders)
            {
                if (folderSet.GetType() != typeof(XmlElement)) { continue; }
                // ----------------------------------------------------------------------------------------------------------------
                //   Create and initialize a structure which holds all attributes for a folderset. Initializing is done via
                //   inheritance of the default attributes structure. This structure in turn  will be inherited by all folders
                //   specified in this set
                // ----------------------------------------------------------------------------------------------------------------
                scanAttributes folderSetAttr = new scanAttributes();
                folderSetAttr = defAttributes;
                string level1PreAction        = "";
                string level1PostAction       = "";
                // ----------------------------------------------------------------------------------------------------------------
                //   Check the schedule & system. Only continue when the folderset has the correct or no schedule/system
                // ----------------------------------------------------------------------------------------------------------------
                foreach (XmlAttribute attribute in folderSet.Attributes)
                {
                    switch (attribute.Name.ToLower())
                    {
                        case "deleteemptyfolders": folderSetAttr.deleteEmptyFolders = Convert.ToBoolean(folderSet.GetAttribute(attribute.Name)); break;
                        case "exclude":            folderSetAttr.excludeFromScan    = new Regex(fcu.ReplaceKeyword(folderSet.GetAttribute(attribute.Name)), RegexOptions.IgnoreCase); break;
                        case "age":                folderSetAttr.fileAge            = folderSet.GetAttribute(attribute.Name); break;
                        case "check":              folderSetAttr.checkType          = folderSet.GetAttribute(attribute.Name).ToLower()[0]; break;
                        case "forceoverwrite":     folderSetAttr.forceOverWrite     = Convert.ToBoolean(folderSet.GetAttribute(attribute.Name)); break;
                        case "postaction":         level1PostAction                 = folderSet.GetAttribute(attribute.Name); break;
                        case "preaction":          level1PreAction                  = folderSet.GetAttribute(attribute.Name); break;
                        case "processhiddenfiles": folderSetAttr.processHiddenFiles = Convert.ToBoolean(folderSet.GetAttribute(attribute.Name)); break;
                        case "recursive":          folderSetAttr.recursiveScan      = Convert.ToBoolean(folderSet.GetAttribute(attribute.Name)); break;
                        case "archivepath":        folderSetAttr.archivePath        = folderSet.GetAttribute(attribute.Name); break;
                        case "schedule":           folderSetAttr.scheduleName       = folderSet.GetAttribute(attribute.Name); break;
                        case "system":             folderSetAttr.systemName         = new Regex(fcu.ReplaceKeyword(folderSet.GetAttribute(attribute.Name)), RegexOptions.IgnoreCase); break;
                        default: total.Logger.Error("Unknown attribute \"" + attribute.Name + "\" found in Folders set"); Environment.Exit(1); break;
                    }
                }
                ValidateFileCheck(folderSetAttr.checkType, "<Folders>");
                if (String.IsNullOrEmpty(folderSetAttr.scheduleName)) { folderSetAttr.scheduleName = scheduleName; }
                if ((scheduleName != defAttributes.scheduleName) && (folderSetAttr.scheduleName != scheduleName)) { continue; }
                if (!folderSetAttr.systemName.Match(total.ENV.Server).Success) { continue; }
                //if (total.LoggerIsDebugEnabled) { ShowFolderAttributes(folderSetAttr); }
                // ----------------------------------------------------------------------------------------------------------------
                //   OK, this folderset is valid for this server at this time, get the individual folder entries
                //   Use a copy of the folderSetAttr struct as starting point forthe individual folder attributes
                //   If specified, perform the preAction before entering the Folder/FolderSet loop
                // ----------------------------------------------------------------------------------------------------------------
                if (level1PreAction.Length > 0) { ExecutePrePostAction(level1PreAction, "preAction"); }
                // ----------------------------------------------------------------------------------------------------------------
                //   Start processing the defined folders in this folderset. Copy the attributes from the folderSet
                //   ....with exception of the pre/post actions!
                // ----------------------------------------------------------------------------------------------------------------
                foreach (var folderElement in folderSet)
                {
                    if (folderElement.GetType() != typeof(XmlElement)) { continue; }
                    XmlElement fcuFolder = (XmlElement)folderElement;
                    scanAttributes folderAttr = new scanAttributes();
                    folderAttr = folderSetAttr;
                    string level2PostAction    = "";
                    string level2PreAction     = "";
                    string scanPath            = "";
                    foreach (XmlAttribute attribute in fcuFolder.Attributes)
                    {
                        switch (attribute.Name.ToLower())
                        {
                            case "path":               scanPath                      = fcu.ReplaceKeyword(fcuFolder.GetAttribute(attribute.Name)); break;
                            case "deleteemptyfolders": folderAttr.deleteEmptyFolders = Convert.ToBoolean(fcuFolder.GetAttribute(attribute.Name)); break;
                            case "exclude":            folderAttr.excludeFromScan    = new Regex(fcu.ReplaceKeyword(fcuFolder.GetAttribute(attribute.Name)), RegexOptions.IgnoreCase); break;
                            case "age":                folderAttr.fileAge            = fcuFolder.GetAttribute(attribute.Name); break;
                            case "check":              folderAttr.checkType          = fcuFolder.GetAttribute(attribute.Name).ToLower()[0]; break;
                            case "forceoverwrite":     folderAttr.forceOverWrite     = Convert.ToBoolean(fcuFolder.GetAttribute(attribute.Name)); break;
                            case "postaction":         level2PostAction              = fcuFolder.GetAttribute(attribute.Name); break;
                            case "preaction":          level2PreAction               = fcuFolder.GetAttribute(attribute.Name); break;
                            case "processhiddenfiles": folderAttr.processHiddenFiles = Convert.ToBoolean(fcuFolder.GetAttribute(attribute.Name)); break;
                            case "recursive":          folderAttr.recursiveScan      = Convert.ToBoolean(fcuFolder.GetAttribute(attribute.Name)); break;
                            case "archivepath":        folderAttr.archivePath        = fcuFolder.GetAttribute(attribute.Name); break;
                            case "name":               folderAttr.fileName           = fcuFolder.GetAttribute(attribute.Name); break;
                            case "schedule":           folderAttr.scheduleName       = fcuFolder.GetAttribute(attribute.Name); break;
                            case "system":             folderAttr.systemName         = new Regex(fcu.ReplaceKeyword(fcuFolder.GetAttribute(attribute.Name)), RegexOptions.IgnoreCase); break;
                            default: total.Logger.Error("Unknown attribute \"" + attribute.Name + "\" found in Folder node"); Environment.Exit(1); break;
                        }
                    }
                    ValidateFileCheck(folderAttr.checkType, "<Folder>");
                    //if (total.LoggerIsDebugEnabled) { ShowFolderAttributes(folderAttr); }
                    // ------------------------------------------------------------------------------------------------------------
                    //   Now we have all active attributes for this path. Again check server & schedule
                    // ------------------------------------------------------------------------------------------------------------
                    if (String.IsNullOrEmpty(folderAttr.scheduleName)) { folderAttr.scheduleName = scheduleName; }
                    if ((scheduleName != defAttributes.scheduleName) && (folderAttr.scheduleName != scheduleName)) { continue; }
                    if (!folderAttr.systemName.Match(total.ENV.Server).Success) { continue; }
                    total.Logger.Debug("Processing Path: " + scanPath);
                    // ------------------------------------------------------------------------------------------------------------
                    //   Time to collect the file cleaup details (name, etention, age, action, etc.)
                    //   If specified, perform the preAction before entering the File/Folder loop
                    // ------------------------------------------------------------------------------------------------------------
                    if (level2PreAction.Length > 0) { ExecutePrePostAction(level2PreAction, "preAction"); }
                    // ------------------------------------------------------------------------------------------------------------
                    //   Start processing the defined files in this folder definition
                    // ------------------------------------------------------------------------------------------------------------
                    foreach (var fileElement in fcuFolder)
                    {
                        if (fileElement.GetType() != typeof(XmlElement)) { continue; }
                        XmlElement fileDef = (XmlElement)fileElement;
                        scanAttributes fileAttr = new scanAttributes();
                        fileAttr = folderAttr;
                        // Non-inheritable attributes
                        fileAttr.actionName      = "";
                        fileAttr.actionTarget    = "";
                        fileAttr.fileName        = "";
                        string level3PostAction  = "";
                        string level3PreAction   = "";
                        foreach (XmlAttribute attribute in fileDef.Attributes)
                        {
                            switch (attribute.Name.ToLower())
                            {
                                case "deleteemptyfolders": fileAttr.deleteEmptyFolders = Convert.ToBoolean(fileDef.GetAttribute(attribute.Name)); break;
                                case "exclude":            fileAttr.excludeFromScan    = new Regex(fcu.ReplaceKeyword(fileDef.GetAttribute(attribute.Name)), RegexOptions.IgnoreCase); break;
                                case "forceoverwrite":     fileAttr.forceOverWrite     = Convert.ToBoolean(fileDef.GetAttribute(attribute.Name)); break;
                                case "postaction":         level3PostAction            = fileDef.GetAttribute(attribute.Name); break;
                                case "preaction":          level3PreAction             = fileDef.GetAttribute(attribute.Name); break;
                                case "processhiddenfiles": fileAttr.processHiddenFiles = Convert.ToBoolean(fileDef.GetAttribute(attribute.Name)); break;
                                case "recursive":          fileAttr.recursiveScan      = Convert.ToBoolean(fileDef.GetAttribute(attribute.Name)); break;
                                case "archivepath":        fileAttr.archivePath        = fileDef.GetAttribute(attribute.Name); break;
                                case "name":               fileAttr.fileName           = fileDef.GetAttribute(attribute.Name); break;
                                case "age":                fileAttr.fileAge            = fileDef.GetAttribute(attribute.Name); break;
                                case "check":              fileAttr.checkType          = fileDef.GetAttribute(attribute.Name).ToLower()[0]; break;
                                case "action":             fileAttr.actionName         = fileDef.GetAttribute(attribute.Name); break;
                                case "target":             fileAttr.actionTarget       = fileDef.GetAttribute(attribute.Name); break;
                                case "schedule":           fileAttr.scheduleName       = fileDef.GetAttribute(attribute.Name); break;
                                case "system":             fileAttr.systemName         = new Regex(fcu.ReplaceKeyword(fileDef.GetAttribute(attribute.Name)), RegexOptions.IgnoreCase); break;
                                default: total.Logger.Error("Unknown attribute \"" + attribute.Name + "\" found in File node"); Environment.Exit(1); break;
                            }
                        }
                        ValidateFileCheck(fileAttr.checkType, "<File>");
                        // --------------------------------------------------------------------------------------------------------
                        //   Resolve the path as it might contain wildcards. Foreach path found, scan it for files matching
                        //   the collected file attributes. Skip the folder when excluded!
                        // --------------------------------------------------------------------------------------------------------
                        string[] pathList = total.ResolvePath(scanPath);
                        foreach (string filePath in pathList)
                        {
                            if (restrictedPaths.Contains(filePath, StringComparer.CurrentCultureIgnoreCase)) { total.Logger.Warn("Skipping restricted path: " + filePath); continue; }
                            if (folderAttr.excludeFromScan.Match(filePath).Success) { total.Logger.Info("Excluding path: " + filePath); continue; }
                            List<string> allFiles = new List<string>();
                            List<FileInfo> fcuFiles = new List<FileInfo>();
                            if (!fileAttr.systemName.Match(total.ENV.Server).Success) { continue; }
                            SearchOption recursiveScan = SearchOption.TopDirectoryOnly;
                            if (fileAttr.recursiveScan) { recursiveScan = SearchOption.AllDirectories; }
                            // ----------------------------------------------------------------------------------------------------
                            // fileAttr.fileName can be a comma-separated set of filenames. Make sure not to skip any of them
                            // ----------------------------------------------------------------------------------------------------
                            foreach (string fileName in fileAttr.fileName.Split(','))
                            {
                                total.Logger.Info("Scanning path: " + filePath + " for: " + fileName);
                                allFiles.AddRange(GetAllFilesFromFolder(filePath, recursiveScan, fileName));
                            }
                            // ----------------------------------------------------------------------------------------------------
                            //   Save file info details in the INF structure so it can be used by other functions
                            // ----------------------------------------------------------------------------------------------------
                            INF.filePath = filePath;
                            // ----------------------------------------------------------------------------------------------------
                            //   Now for all files found in this path;
                            //   - skip if the file is already gone (temp files have this habbit ;)
                            //   - skip if the exclude Regex matches
                            //   - skip if its age does not match (LastWriteTime or CreationTime)
                            //   - skip if file is hidden and processHiddenFiles is false
                            //   - skip if action is compact and file is already compacted
                            //   - skip if path is a DFSR Private path (*\DfsrPrivate\*)
                            // ----------------------------------------------------------------------------------------------------
                            DateTime fileDateCheck = DateTime.Now.AddDays(Convert.ToDouble(fileAttr.fileAge)*-1);
                            foreach (string fcuFile in allFiles.Distinct())
                            {
                                if (fcuFile.Contains("\\DfsrPrivate\\")) { continue; }
                                if (!File.Exists(fcuFile)) { continue; }
                                if (fileAttr.excludeFromScan.Match(fcuFile).Success) { total.Logger.Debug("Skipping excluded file \"" + fcuFile + "\"");continue; }
                                bool fileIsHidden     = ((File.GetAttributes(fcuFile) & FileAttributes.Hidden)     == FileAttributes.Hidden);
                                bool fileIsCompressed = ((File.GetAttributes(fcuFile) & FileAttributes.Compressed) == FileAttributes.Compressed);
                                if (fileIsHidden & !folderAttr.processHiddenFiles) { total.Logger.Debug("Skipping hidden file \"" + fcuFile + "\""); continue; }
                                if (fileIsCompressed & (fileAttr.actionName.ToLower() == "compact")) { total.Logger.Debug("Skipping compressed file \"" + fcuFile + "\""); continue; }
                                FileInfo fileInfo = new FileInfo(fcuFile);
                                INF.fileName     = fileInfo.Name;
                                INF.fileExt      = fileInfo.Extension;
                                INF.fileBaseName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                                // ------------------------------------------------------------------------------------------------
                                //   File matches all prerequisites, 1 final check: match file create or modify date?
                                //   If it matches the correct date criteria add it to the list
                                // ------------------------------------------------------------------------------------------------
                                switch (fileAttr.checkType)
                                {
                                    case 'c': if (fileInfo.CreationTime  < fileDateCheck) { fcuFiles.Add(fileInfo); } break;
                                    case 'm': if (fileInfo.LastWriteTime < fileDateCheck) { fcuFiles.Add(fileInfo); } break;
                                }
                            }
                            // ----------------------------------------------------------------------------------------------------
                            //    If there are files to process, pass the list of files to the action routine of choice
                            //    Do not forget to perform possible Pre & Post actions
                            // ----------------------------------------------------------------------------------------------------
                            if (fcuFiles.Count > 0)
                            {
                                // ------------------------------------------------------------------------------------------------
                                //   Is there a PreAction to perform?
                                // ------------------------------------------------------------------------------------------------
                                if (level3PreAction.Length > 0) { ExecutePrePostAction(level3PreAction, "preAction"); }
                                // ------------------------------------------------------------------------------------------------
                                //   Now execute the requested action, clear per folder counters before proceeding
                                // ------------------------------------------------------------------------------------------------
                                folder_bytesZipped = 0;
                                folder_CompressRatio = 0;
                                folder_bytesCompacted = 0;
                                folder_CompationRatio = 0;
                                folder_bytesArchived = 0;
                                folder_bytesDeleted = 0;
                                folder_bytesMoved = 0;
                                switch (fileAttr.actionName.ToLower())
                                {
                                    case "compact":   fcu.CompactFilesInList(fcuFiles); break;
                                    case "delete":    fcu.DeleteFilesInList(fcuFiles); break;
                                    case "move":      fcu.MoveFilesInList(fcuFiles, fileAttr); break;
                                    case "archive":   fcu.ArchiveFilesInList(fcuFiles, fileAttr); break;
                                    default: total.Logger.Error("Invalid action \"" + fileAttr.actionName + "\" found for " + scanPath); break;
                                }
                                // ------------------------------------------------------------------------------------------------
                                //   Is there a PostAction to perform?
                                // ------------------------------------------------------------------------------------------------
                                if (level3PostAction.Length > 0) { ExecutePrePostAction(level3PostAction, "postAction"); }
                            }
                            // ----------------------------------------------------------------------------------------------------
                            //   Delete empty folders?? Lets do so now
                            // ----------------------------------------------------------------------------------------------------
                            if (folderAttr.deleteEmptyFolders) { fcu.DeleteEmptyFolders(filePath, folderAttr.excludeFromScan, recursiveScan); }
                            // ----------------------------------------------------------------------------------------------------
                        }
                    }
                    // ------------------------------------------------------------------------------------------------------------
                    //   If specified, perform the postAction before entering a new File/Folder loop
                    // ------------------------------------------------------------------------------------------------------------
                    if (level2PostAction.Length > 0) { ExecutePrePostAction(level2PostAction, "postAction"); }
                }
                // ----------------------------------------------------------------------------------------------------------------
                //   If specified, perform the postAction before entering a new Folder/FolderSet loop
                // ----------------------------------------------------------------------------------------------------------------
                if (level1PostAction.Length > 0) { ExecutePrePostAction(level1PostAction, "postAction"); }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Finish the job by showing how much space has been regained
            // --------------------------------------------------------------------------------------------------------------------
            string prfx = "";
            if (total.APP.Dryrun)
            {
                prfx = " [DRYRUN] - ";
            }
            else
            {
                if (EventLogInitialized)
                {
                    string evtLogMessage = "WinFCU filesystem cleanup run completed";
                    evtLogMessage += String.Format(" - Total bytes deleted  : {0}\n", total_bytesDeleted);
                    evtLogMessage += String.Format(" - Total bytes archived : {0}\n", total_bytesArchived);
                    evtLogMessage += String.Format(" - Total bytes moved    : {0}\n", total_bytesMoved);
                    evtLogMessage += String.Format(" - Total bytes compacted: {0}\n", total_bytesCompacted);
                    evtLog.WriteEntry(evtLogMessage, EventLogEntryType.Information, 0);
                }
            }
            total.Logger.Info(dash80);
            total.Logger.Info(prfx + "Total bytes deleted  : " + total_bytesDeleted);
            total.Logger.Info(prfx + "Total bytes archived : " + total_bytesArchived);
            total.Logger.Info(prfx + "Total bytes moved    : " + total_bytesMoved);
            total.Logger.Info(prfx + "Total bytes compacted: " + total_bytesCompacted);
            total.Logger.Info(dash80);
            // --------------------------------------------------------------------------------------------------------------------
            //   Finaly perform some memory cleanup by calling the garbage collector
            // --------------------------------------------------------------------------------------------------------------------
            GC.Collect();
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Replace all known WinFCU keywords in the provided string, and finally call the generic ReplaceKeyword function
        // ------------------------------------------------------------------------------------------------------------------------
        public static string ReplaceKeyword(string SourceString)
        {
            string Record = SourceString;
            // --------------------------------------------------------------------------------------------------------------------
            //   Load the current content of the app sortedlist.
            // --------------------------------------------------------------------------------------------------------------------
            total.DTM.Year = String.Format("{0:yyyy}", DateTime.Now);
            total.DTM.Month = String.Format("{0:yyyyMM}", DateTime.Now);
            total.DTM.Date = String.Format("{0:yyyyMMdd}", DateTime.Now);
            total.DTM.Time = String.Format("{0:HHmmss}", DateTime.Now);
            total.DTM.DateTime = String.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
            // --------------------------------------------------------------------------------------------------------------------
            //   This will only work if there is an 'active' file. If not we need to fake one so we can at least
            //   show the keyword definitions when requested
            // --------------------------------------------------------------------------------------------------------------------
            if (INF.fileName == null) { SetKeywordValues(total.APP.FileInfo); }
            if (fcu.runLogFile == null) { fcu.runLogFile = total.APP.LogFile; }
            // --------------------------------------------------------------------------------------------------------------------
            //   Now update the keyword list
            // --------------------------------------------------------------------------------------------------------------------
            total.AddReplacementKeyword("#FCULOGFILE#", fcu.runLogFile, "WINFCU: The current WinFCU logfile");
            total.AddReplacementKeyword("#FCULOGDIR#", Path.GetDirectoryName(fcu.runLogFile), "WINFCU: The current WinFCU logfile path");
            total.AddReplacementKeyword("#SCHEDULE#", INF.scheduleName, "WINFCU: The active WinFCU schedule(s)");
            total.AddReplacementKeyword("#PATH#", INF.filePath, "WINFCU: File path of currently processed file");
            total.AddReplacementKeyword("#FNAME#", INF.fileName, "WINFCU: Filename of currently processed file");
            total.AddReplacementKeyword("#FBNAME#", INF.fileBaseName, "WINFCU: Filename without extension of currently processed file");
            total.AddReplacementKeyword("#FEXT#", INF.fileExt, "WINFCU: Extension of currently processed file");
            total.AddReplacementKeyword("#FRDIR#", INF.fileRdir[0], "WINFCU: File subdir name level 0 of currently processed file (equals #FR0DIR#)");
            total.AddReplacementKeyword("#FSDIR#", INF.fileSdir[0], "WINFCU: File reverse subdir name level 0 of currently processed file (equals #FS0DIR#)");
            total.AddReplacementKeyword("#FDIR#", INF.fileSdir[0], "WINFCU: File reverse subdir name level 0 of currently processed file");
            for (int i = 0; i <= 9; i++)
            {
                string rplKW = String.Format("#FR{0}DIR#", i);
                total.AddReplacementKeyword(rplKW, INF.fileRdir[i], "WINFCU: File subdir name level " + i + " of currently processed file");
                rplKW = String.Format("#FS{0}DIR#", i);
                total.AddReplacementKeyword(rplKW, INF.fileSdir[i], "WINFCU: File reverse subdir name level " + i + " of currently processed file");
            }
            total.AddReplacementKeyword("#FCDATE#", INF.createDate, "WINFCU: Creation date (yyyyMMdd) of currently processed file");
            total.AddReplacementKeyword("#FCMONTH#", INF.createMonth, "WINFCU: Creation month (yyyyMM) of currently processed file");
            total.AddReplacementKeyword("#FCYEAR#", INF.createYear, "WINFCU: Creation year (yyyyMM) of currently processed file");
            total.AddReplacementKeyword("#FMDATE#", INF.modifyDate, "WINFCU: Last write date (yyyyMMdd) of currently processed file");
            total.AddReplacementKeyword("#FMMONTH#", INF.modifyMonth, "WINFCU: Last write month (yyyyMM) of currently processed file");
            total.AddReplacementKeyword("#FMYEAR#", INF.modifyYear, "WINFCU: Last write year (yyyy) of currently processed file");
            total.AddReplacementKeyword("#FADATE#", INF.accessDate, "WINFCU: Last access date (yyyyMMdd) of currently processed file");
            total.AddReplacementKeyword("#FAMONTH#", INF.accessMonth, "WINFCU: Last access month (yyyyMM) of currently processed file");
            total.AddReplacementKeyword("#FAYEAR#", INF.accessYear, "WINFCU: Last access year (yyyy) of currently processed file");
            // --------------------------------------------------------------------------------------------------------------------
            //   Now call the 'generic' Replacekeyword function and return the result
            // --------------------------------------------------------------------------------------------------------------------
            return total.ReplaceKeyword(Record);
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Empty the recycle bins (if wanted than to be called at the end of a cleanup run)
        // ------------------------------------------------------------------------------------------------------------------------
        public static void EmptyRecycleBins()
        {
            // --------------------------------------------------------------------------------------------------------------------
            //   Construct a scanpath for all drives using the path name
            // --------------------------------------------------------------------------------------------------------------------
            try
            {
                foreach (string rbPath in total.ResolvePath("?:\\*recycle*\\"))
                {
                    foreach (string rbFile in GetAllFilesFromFolder(rbPath, SearchOption.AllDirectories, "*")) { File.Delete(rbFile); }
                }
                total.Logger.Info("The recycle bins have been emptied, all clear now.");
                total.Logger.Info(new string('-', 80));
            }
            catch (Exception ex) { total.Logger.Error("Failed to empty the recycle bins, must be done manually!\n", ex); total.Logger.Info(new string('-', 80)); }
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Invoke a PowerShell command/script and return the results as a list of strings
        // ------------------------------------------------------------------------------------------------------------------------
        public static List<string> InvokePowerShell(string psScript)
        {
            List <string> psResults = new List<string>();
            using (PowerShell PowerShellInstance = PowerShell.Create())
            {
                // prepare the execution pipeline
                PowerShellInstance.AddScript(psScript);
                // invoke execution on the pipeline (collecting output)
                Collection<PSObject> PSOutput = PowerShellInstance.Invoke();
                PowerShellInstance.Dispose();
                // loop through each output object item
                foreach (PSObject outputItem in PSOutput)
                {
                    // if null object was dumped to the pipeline during the script then a null
                    // object may be present here. check for null to prevent potential NRE.
                    if (outputItem != null)
                    {
                        //TODO: do something with the output item 
                        // outputItem.BaseOBject
                        psResults.Add(outputItem.BaseObject.ToString());
                    }
                }
            }
            return psResults;
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Load the given config file intpo an XmlDocument. Max 4 attempts before failing
        // ------------------------------------------------------------------------------------------------------------------------
        private static XmlDocument LoadXmlDocument(string configFileName)
        {
            XmlDocument xmlDocument = new XmlDocument();
            Exception loadException = new Exception();
            int loadAttempt = 0;
            // ----------------------------------------------------------------------------------------------------
            //   Try loading the file, catch any exception
            // ----------------------------------------------------------------------------------------------------
            while (loadAttempt < 4)
            {
                try { xmlDocument.Load(configFileName); break; }
                catch (Exception ex)
                {
                    loadAttempt += 1;
                    loadException = ex;
                    total.Logger.Debug(" - problem loading include file, retry attempt #" + loadAttempt + "\n", loadException);
                    System.Threading.Thread.Sleep(1000);
                }
            }
            // ----------------------------------------------------------------------------------------------------
            //   Failed to load the file? Then say so and quit. Else return its content as an Xml Document
            // ----------------------------------------------------------------------------------------------------
            if (loadAttempt >= 4)
            {
                total.Logger.Fatal("Fatal error loading include file " + configFileName + "\n", loadException);
                Environment.Exit(1);
            }
            return xmlDocument;
        }

    }
}
