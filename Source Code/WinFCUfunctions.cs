using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using System.Xml;
using Total.Util;
using log4net.Appender;
using Total.CLI;
using log4net.Core;

namespace Total.WinFCU
{
    public partial class fcu
    {
        public static string prcInfo = String.Format(" WinFCU Run Details:\r\n{0}\r\n", new string ('=', 80));
        private static scanAttributes defAttributes = new scanAttributes();
        private static string WildcardToRegex(string pattern) { return "^" + pattern.Replace(@".", @"\.").Replace(@"*", @".*").Replace(@"?", @".") + "$"; }
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
                catch (DirectoryNotFoundException exDNF) { total.Logger.Debug(exDNF.Message); continue; }
                catch (UnauthorizedAccessException exUAE) { total.Logger.Debug(exUAE.Message); continue; }
                catch (ArgumentException exAE) { total.Logger.Debug(exAE.Message.TrimEnd('.') + " \"" + currentFolder + "\""); continue; }
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
        private static void ShowFolderAttributes(scanAttributes fa)
        {
            total.Logger.Debug("Attributes for the current FolderSet:");
            total.Logger.Debug("-------------------------------------");
            total.Logger.Debug(" - processHiddenFiles: " + fa.processHiddenFiles);
            total.Logger.Debug(" - deleteEmptyFolders: " + fa.deleteEmptyFolders);
            total.Logger.Debug(" - forceOverWrite:     " + fa.forceOverWrite);
            total.Logger.Debug(" - recursiveScan:      " + fa.recursiveScan);
            total.Logger.Debug(" - excludePath:        " + fa.excludeFromScan);
            total.Logger.Debug(" - scheduleName:       " + fa.scheduleName);
            total.Logger.Debug(" - systemName:         " + fa.systemName);
        }
        private static void ExecutePrePostAction(string Action, string Type)
        {
            string Message = String.Format("Performing {0}: {1}", Type, Action);
            if (total.APP.Dryrun) { total.Logger.Info(" [DRYRUN] - " + Message); }
            else
            {
                total.Logger.Info(Message);
                try
                {
                    PowerShell ps = PowerShell.Create().AddScript(Action);
                    foreach (PSObject result in ps.Invoke()) { if (total.Logger.IsDebugEnabled) { total.Logger.Debug(String.Format("{0} result: {1}", Type, result.BaseObject.ToString())); } }
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
            //   Now replace know keywords in string
            // --------------------------------------------------------------------------------------------------------------------
            INF.accessDate = fileInfo.LastAccessTime.ToString("yyyyMMdd");
            INF.accessMonth = INF.accessDate.Substring(0, 6);
            INF.accessYear = INF.accessDate.Substring(0, 4);
            INF.createDate = fileInfo.CreationTime.ToString("yyyyMMdd");
            INF.createMonth = INF.createDate.Substring(0, 6);
            INF.createYear = INF.createDate.Substring(0, 4);
            INF.modifyDate = fileInfo.LastWriteTime.ToString("yyyyMMdd");
            INF.modifyMonth = INF.modifyDate.Substring(0, 6);
            INF.modifyYear = INF.modifyDate.Substring(0, 4);
            // --------------------------------------------------------------------------------------------------------------------
            //   Breakdown the filepath info separate folders keywords (max 10)
            // --------------------------------------------------------------------------------------------------------------------
            string[] subDirs = fileInfo.DirectoryName.Split('\\');
            INF.fileDirCount = (subDirs.Length > 9) ? 9 : subDirs.Length;
            for (int i = 0; i < INF.fileDirCount; i++)
            {
                INF.fileRdir[i] = subDirs[i];
                INF.fileSdir[i] = subDirs[INF.fileDirCount - i - 1];
            }
            // --------------------------------------------------------------------------------------------------------------------
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Load process, environment, configuration and other details into local storage
        //   Beware of Inheritable and Non-Inheritable attributes!
        // ------------------------------------------------------------------------------------------------------------------------
        public static void LoadConfiguration(string fcuConfigFile)
        {
            string xmlTransNode = "/*[translate(local-name(),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='{0}']";
            string foldersQuery = String.Format(xmlTransNode, "configuration") +
                                  String.Format(xmlTransNode, "applicationsettings") +
                                  String.Format(xmlTransNode, "folders");
            Dictionary<char, string> checkType = new Dictionary<char, string>() { { 'm', "Modified" }, { 'c', "Created" } };
            // --------------------------------------------------------------------------------------------------------------------
            //   Check the existance of the config file and if it does not exist - Quit!
            //   OBSOLETE - If existing, encrypt accounts/passwords at the specified location using ReplaceXMLCredentials
            // --------------------------------------------------------------------------------------------------------------------
            fcu.runConfig = fcu.ReplaceKeyword(fcuConfigFile);
            if (!File.Exists(fcu.runConfig)) { total.Logger.Error("Config file \"" + fcu.runConfig + "\" not found, terminating now..."); Environment.Exit(1); }
            //total.ReplaceXMLCredentials(fcu.runConfig, "configuration.applicationsettings.appsettings.security", "account", "password", "cred");
            // --------------------------------------------------------------------------------------------------------------------
            //  Read the file content and return it as an XML document
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading primary config file: " + fcu.runConfig);
            fcu.fcuConfig = new XmlDocument();
            fcu.fcuConfig.Load(fcu.runConfig);
            XmlNodeList fcuNodeList = fcu.fcuConfig.SelectNodes(foldersQuery);
            foreach (XmlElement fcuNode in fcuNodeList) { fcuNode.SetAttribute("configname", Path.GetFileName(fcu.runConfig)); }
            // --------------------------------------------------------------------------------------------------------------------
            //   Restricted paths! - Some paths you just do not want to be cleaned. Paths like C:\, C:\Windows, C:\Users, etc.
            //   The restrictedPath is preloaded with some path WinFCU will not clean by default.
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading Restricted Path definitions");
            string rpQuery = String.Format(xmlTransNode, "configuration") +
                             String.Format(xmlTransNode, "applicationsettings") +
                             String.Format(xmlTransNode, "restrictedpaths") +
                             String.Format(xmlTransNode, "add");
            // First of all preload the restricted path list
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
            //   The usrKeywords SortedList is used by total.ReplaceKeyword !!
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading User Keyword definitions");
            string kwQuery = String.Format(xmlTransNode, "configuration") +
                             String.Format(xmlTransNode, "applicationsettings") +
                             String.Format(xmlTransNode, "keywords") +
                             String.Format(xmlTransNode, "add");
            try
            {
                foreach (XmlElement kwNode in fcu.fcuConfig.SelectNodes(kwQuery))
                {
                    string kwKey = ""; string kwValue = "";
                    foreach (XmlAttribute kwAttribute in kwNode.Attributes)
                    {
                        switch (kwAttribute.Name.ToLower())
                        {
                            case "key":   kwKey    = kwAttribute.Value; break;
                            case "value": kwValue = kwAttribute.Value; break;
                            default: total.Logger.Error("Invalid attribute \"" + kwAttribute.Name + "\" found in " + kwNode.OuterXml); break;
                        }
                    }
                    if (kwKey.Length == 0)    { total.Logger.Warn("No key attribute in line: " + kwNode.OuterXml); continue; }
                    if (kwValue.Length == 0) { total.Logger.Warn("No scriptblock attribute in line: " + kwNode.OuterXml); continue; }
                    total.Logger.Debug("Verifying user keyword: " + kwKey);
                    PowerShell ps = PowerShell.Create().AddScript(kwValue);
                    foreach (PSObject result in ps.Invoke()) { total.usrKeywords[kwKey] = result.BaseObject.ToString(); }
                }
            }
            catch (Exception ex) { total.Logger.Fatal("Error processing WinFCU User Keywords.\n", ex); Environment.Exit(1); }
            // --------------------------------------------------------------------------------------------------------------------
            //   XML query for Include, one or more configuration files can be loaded.
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading Include file(s)");
            string incFileLoading = "";
            string incQuery = String.Format(xmlTransNode, "configuration") +
                              String.Format(xmlTransNode, "applicationsettings") +
                              String.Format(xmlTransNode, "includefiles");
            try
            {
                string includePath = ""; string includeFile = "";
                foreach (XmlAttribute incAttribute in fcu.fcuConfig.SelectSingleNode(incQuery).Attributes)
                {
                    switch (incAttribute.Name.ToLower())
                    {
                        case "path": includePath = incAttribute.Value; break;
                        default: total.Logger.Error("Invalid attribute \"" + incAttribute.Name + "\" found in includeFiles"); break;
                    }
                }
                if (includePath.Length == 0) { total.Logger.Warn("No includeFiles path specified."); return; }
                includePath = fcu.ReplaceKeyword(includePath);
                if (!Path.IsPathRooted(includePath)) { includePath = Path.Combine(total.APP.Path, includePath); }
                includeFile = Path.GetFileName(includePath);
                if (includeFile.Length == 0) { includeFile = "*.config"; }
                includePath = Path.GetDirectoryName(includePath);
                DirectoryInfo incDirInfo = new DirectoryInfo(includePath);
                if (!incDirInfo.Exists) { total.Logger.Error("Specified includeFiles path does not exist."); return; }
                FileInfo[] incFileInfo = incDirInfo.GetFiles(includeFile);
                foreach (FileInfo incFile in incFileInfo)
                {
                    incFileLoading = incFile.FullName;
                    fcu.incConfig.Add(incFileLoading);
                    total.Logger.Debug("Processing include file " + incFileLoading);
                    XmlDocument incConfig = new XmlDocument();
                    incConfig.Load(incFileLoading);
                    fcuNodeList = incConfig.SelectNodes(foldersQuery);
                    foreach (XmlElement fcuNode in fcuNodeList) { fcuNode.SetAttribute("configname", incFile.Name); }
                    foreach (XmlNode node in incConfig.DocumentElement.ChildNodes)
                    {
                        XmlNode imported = fcu.fcuConfig.ImportNode(node, true);
                        fcu.fcuConfig.DocumentElement.AppendChild(imported);
                    }
                }
            }
            catch (Exception ex) { total.Logger.Fatal("Error processing include file \"" + incFileLoading +"\"", ex); Environment.Exit(1); }
            // --------------------------------------------------------------------------------------------------------------------
            //   OBSOLETE - Check for security settings. Credential replacement has already been done, so just get the encrypted string
            // --------------------------------------------------------------------------------------------------------------------
            //total.Logger.Debug("Loading Security settings");
            //string secQuery = string.Format(xmlTransNode, "configuration") +
            //                  String.Format(xmlTransNode, "applicationsettings") +
            //                  String.Format(xmlTransNode, "appsettings") +
            //                  String.Format(xmlTransNode, "security");
            //try
            //{
            //    XmlNode secNode = fcu.fcuConfig.SelectSingleNode(secQuery);
            //    if ((secNode != null) && (secNode.Attributes[0].Value.Length > 0)) { fcu.secCredentials = secNode.Attributes[0].Value; }
            //}
            //catch (Exception ex) { total.Logger.Fatal("Error processing Security settings.\n", ex); Environment.Exit(1); }
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
                    switch (defValue) {
                        case "debug":    total.APP.Debug  = attribute.Value.ToLower() == "true"; break;
                        case "dryrun":   total.APP.Dryrun = attribute.Value.ToLower() == "true"; break;
                        default:         total.Logger.Error("Invalid RunOption setting " + defValue + " detected, terminating process"); break;
                    }
                }
                if (cli.IsPresent("Dryrun")) { total.APP.Dryrun = Convert.ToBoolean(cli.GetValue("Dryrun")); }
                if (cli.IsPresent("Debug"))  { total.APP.Debug  = Convert.ToBoolean(cli.GetValue("Debug")); }
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
            defAttributes.excludeFromScan    = new Regex("##", RegexOptions.IgnoreCase);
            // Inheritable
            defAttributes.deleteEmptyFolders = false;
            defAttributes.fileAge            = "10";
            defAttributes.checkType          = 'm';
            defAttributes.forceOverWrite     = false;
            defAttributes.processHiddenFiles = false;
            defAttributes.recursiveScan      = false;
            defAttributes.scheduleName       = "#ALL#";
            defAttributes.systemName         = new Regex(".*", RegexOptions.IgnoreCase);
            string defQuery = String.Format(xmlTransNode, "configuration") +
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
                        case "schedule":           defAttributes.scheduleName       = attribute.Value; break;
                        case "system":             defAttributes.systemName         = new Regex(attribute.Value, RegexOptions.IgnoreCase); break;
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
            //   The logFile is no longer a RunOption, this is now done by log4net. So ask log4net for its name!
            // --------------------------------------------------------------------------------------------------------------------
            foreach (IAppender l4nAppender in (total.Logger.Logger.Repository.GetAppenders()))
            {
                Type t = l4nAppender.GetType();
                if (!t.Equals(typeof(FileAppender)) & !t.Equals(typeof(RollingFileAppender))) { continue; }
                fcu.runLogFile = ((FileAppender)l4nAppender).File;
                break;
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   If schedules are defined, create a combined schedule object. Additional requests are appended
            //   Build the XML query for the scheduler settings, and get them all.
            // --------------------------------------------------------------------------------------------------------------------
            total.Logger.Debug("Loading Schedule definitions");
            string schQuery = String.Format(xmlTransNode, "configuration") +
                              String.Format(xmlTransNode, "applicationsettings") +
                              String.Format(xmlTransNode, "schedules") +
                              String.Format(xmlTransNode, "add");
            runHasSchedule = false;
            try
            {
                foreach (XmlElement schNode in fcu.fcuConfig.SelectNodes(schQuery))
                {
                    total.SCH.Name     = "";
                    total.SCH.Days     = "";
                    total.SCH.Start    = "";
                    total.SCH.End      = "";
                    total.SCH.Interval = "";
                    total.SCH.System   = "";
                    foreach (XmlAttribute schAttribute in schNode.Attributes)
                    {
                        switch (schAttribute.Name.ToLower())
                        {
                            case "name":      total.SCH.Name     = schAttribute.Value; break;
                            case "day":       total.SCH.Days     = schAttribute.Value; break;
                            case "start":     total.SCH.Start    = schAttribute.Value; break;
                            case "end":       total.SCH.End      = schAttribute.Value; break;
                            case "interval":  total.SCH.Interval = schAttribute.Value; break;
                            case "system":    total.SCH.System   = schAttribute.Value; break;
                            default: total.Logger.Error("Invalid schedule attribute \"" + schAttribute.Name + "\" found in " + schNode.OuterXml); break;
                        }
                    }
                    Regex rgxSystem = new Regex(fcu.ReplaceKeyword(total.SCH.System), RegexOptions.IgnoreCase);
                    if ((total.SCH.System == null) || (rgxSystem.Match(total.ENV.Server).Success)) { total.CreateSchedule(); }
                }
                total.GetNextScheduledRunTime();
                runHasSchedule = true;
            }
            catch (ArgumentOutOfRangeException) { runHasSchedule = false; }
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
            //   Lets put all info into the total.APP.LogInfo string so it will be written to the logfile when it is created
            // --------------------------------------------------------------------------------------------------------------------
            if (!total.PRC.Interactive)
            {
                prcInfo += String.Format("\r\n WinFCU Process Info\r\n {0}\r\n", new string('-', 78));
                prcInfo += String.Format("  - Process ID           : {0,-10}- ParentID            : {1}\r\n", total.PRC.ProcessID, total.PRC.ParentID);
                prcInfo += String.Format("  - Process Type ID      : {0,-10}- Logon Type Name     : {1}\r\n", total.PRC.Type, total.PRC.TypeName);
                prcInfo += String.Format("  - x64bit OS            : {0,-10}- x64bit Mode         : {1}\r\n", total.ENV.x64os, total.ENV.x64mode);
                prcInfo += String.Format("  - Interactive          : {0,-10}- Service             : {1}\r\n", total.PRC.Interactive, total.PRC.Service);
                prcInfo += String.Format("  - Network              : {0,-10}- Batch               : {1}\r\n", total.PRC.Network, total.PRC.Batch);
            }

            prcInfo += String.Format("\r\n WinFCU Configuration Info\r\n {0}\r\n", new string('-', 78));
            prcInfo += String.Format("  - Logfile              : {0}\r\n", fcu.runLogFile);
            prcInfo += String.Format("  - Config file          : {0}\r\n", fcu.runConfig);
            string showInclude = "  - Include file(s)      : {0}\r\n";
            foreach (string incFile in fcu.incConfig)
            {
                prcInfo += String.Format(showInclude, incFile);
                showInclude = "                         : {0}\r\n";
            }

            prcInfo += String.Format("\r\n WinFCU Runtime Options\r\n {0}\r\n", new string('-', 78));
            prcInfo += String.Format("  - Debug                : {0}\r\n", total.APP.Debug);
            prcInfo += String.Format("  - Dryrun               : {0}\r\n", total.APP.Dryrun);

            prcInfo += String.Format("\r\n WinFCU Restricted Paths\r\n {0}\r\n", new string('-', 78));
            string showResPath = "  - Restricted path(s)   : {0}\r\n";
            foreach (string resPath in restrictedPaths)
            {
                prcInfo += String.Format(showResPath, resPath);
                showResPath = "                         : {0}\r\n";
            }

            prcInfo += String.Format("\r\n WinFCU Inheritable Defaults\r\n {0}\r\n", new string('-', 78));
            prcInfo += String.Format("  - Allowed Systems      : {0}\r\n", defAttributes.systemName);
            prcInfo += String.Format("  - File Age (days)      : {0}\r\n", defAttributes.fileAge);
            prcInfo += String.Format("  - File Age check type  : {0}\r\n", checkType[defAttributes.checkType]);
            prcInfo += String.Format("  - Process hidden files : {0}\r\n", defAttributes.processHiddenFiles);
            prcInfo += String.Format("  - Delete empty folders : {0}\r\n", defAttributes.deleteEmptyFolders);
            prcInfo += String.Format("  - Force overwrite      : {0}\r\n", defAttributes.forceOverWrite);
            prcInfo += String.Format("  - Recursive File Scan  : {0}\r\n", defAttributes.recursiveScan);

            prcInfo += String.Format("\r\n{0}\r\n", new string('=', 80));
            total.Logger.Debug(prcInfo);
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Set a filewatcher on the specified file and return its SID
        // ------------------------------------------------------------------------------------------------------------------------
        //public static string SetFileWatcher(string fcuConfigFile)
        //{
        //    string EventSID = "";
        //    string scanPath = Path.GetDirectoryName(fcuConfigFile);
        //    string fileName = Path.GetFileName(fcuConfigFile);
        //    total.Logger.Debug("Establishing FileSystemWatcher on file: " + fileName +" in path: " + scanPath);
        //    //FileSystemWatcher fileSystemWatcher
        //    return EventSID;
        //}

        // ------------------------------------------------------------------------------------------------------------------------
        //   Show the security credentials (if they have been provided!)
        // ------------------------------------------------------------------------------------------------------------------------
        //public static void ShowCredentials()
        //{
        //    if (fcu.secCredentials.Length == 0) { Console.WriteLine("No Credentials found"); }
        //    string[] scParts = total.DecryptCredentials(fcu.secCredentials);
        //    Console.WriteLine("WinFCU Credentials found for: " + scParts[0]);
        //}

        // ------------------------------------------------------------------------------------------------------------------------
        //   Start cleaning the file system based upon the content of fci.fcuFolders
        // ------------------------------------------------------------------------------------------------------------------------
        public static void CleanFileSystem(string scheduleName = "#ALL#")
        {
            INF.scheduleName = scheduleName;
            int vLen = 16 + total.APP.Version.Length; int cLen = (80 - vLen) / 2;
            string Header = String.Format("{0} W I N F C U - {1} {0}", new String('=', cLen), total.APP.Version);
            if (Header.Length < 80) { Header += "="; }
            total.Logger.Info(Header);
            total.Logger.Info("Starting filesystem cleanup for schedule " + INF.scheduleName);
            total.Logger.Info("--------------------------------------------------------------------------------");
            // --------------------------------------------------------------------------------------------------------------------
            foreach (XmlElement folderSet in fcu.fcuFolders)
            {
                if (folderSet.GetType() != typeof(XmlElement)) { continue; }
                total.Logger.Info("Processing files from: " + folderSet.GetAttribute("configname"));
                // ----------------------------------------------------------------------------------------------------------------
                //   Create and initialize a structure which holds all attributes for a folderset. Initializing is done via
                //   inheritance of the default attributes structure. This structure in turn  will be inherited by all folders
                //   specified in this set
                // ----------------------------------------------------------------------------------------------------------------
                scanAttributes folderSetAttr = new scanAttributes();
                folderSetAttr = defAttributes;
                folderSetAttr.excludeFromScan = new Regex("##", RegexOptions.IgnoreCase);
                string level1PreAction        = "";
                string level1PostAction       = "";
                // ----------------------------------------------------------------------------------------------------------------
                //   Check the schedule & system. Only continue when the folderset has the correct or no schedule/system
                // ----------------------------------------------------------------------------------------------------------------
                foreach (XmlAttribute attribute in folderSet.Attributes)
                {
                    switch (attribute.Name.ToLower())
                    {
                        case "configname":         break;
                        case "deleteemptyfolders": folderSetAttr.deleteEmptyFolders = Convert.ToBoolean(folderSet.GetAttribute(attribute.Name)); break;
                        case "exclude":            folderSetAttr.excludeFromScan    = new Regex(fcu.ReplaceKeyword(folderSet.GetAttribute(attribute.Name)), RegexOptions.IgnoreCase); break;
                        case "age":                folderSetAttr.fileAge            = folderSet.GetAttribute(attribute.Name); break;
                        case "check":              folderSetAttr.checkType          = folderSet.GetAttribute(attribute.Name).ToLower()[0]; break;
                        case "forceoverwrite":     folderSetAttr.forceOverWrite     = Convert.ToBoolean(folderSet.GetAttribute(attribute.Name)); break;
                        case "postaction":         level1PostAction                 = folderSet.GetAttribute(attribute.Name); break;
                        case "preaction":          level1PreAction                  = folderSet.GetAttribute(attribute.Name); break;
                        case "processhiddenfiles": folderSetAttr.processHiddenFiles = Convert.ToBoolean(folderSet.GetAttribute(attribute.Name)); break;
                        case "recursive":          folderSetAttr.recursiveScan      = Convert.ToBoolean(folderSet.GetAttribute(attribute.Name)); break;
                        case "schedule":           folderSetAttr.scheduleName       = folderSet.GetAttribute(attribute.Name); break;
                        case "system":             folderSetAttr.systemName         = new Regex(fcu.ReplaceKeyword(folderSet.GetAttribute(attribute.Name)), RegexOptions.IgnoreCase); break;
                        default: total.Logger.Error("Unknown attribute \"" + attribute.Name + "\" found in Folders set"); Environment.Exit(1); break;
                    }
                }
                ValidateFileCheck(folderSetAttr.checkType, "<Folders>");
                if (folderSetAttr.scheduleName == "") { folderSetAttr.scheduleName = scheduleName; }
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
                    if (folderAttr.scheduleName == "") { folderAttr.scheduleName = scheduleName; }
                    if ((scheduleName != defAttributes.scheduleName) && (folderAttr.scheduleName != scheduleName)) { continue; }
                    if (!folderAttr.systemName.Match(total.ENV.Server).Success) { continue; }
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
                        fileAttr.excludeFromScan = new Regex("##", RegexOptions.IgnoreCase);
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
                            total.Logger.Info("Scanning path: " + filePath + " for: " + fileAttr.fileName);
                            string[] allFiles = null;
                            List<FileInfo> fcuFiles = new List<FileInfo>();
                            if (!fileAttr.systemName.Match(total.ENV.Server).Success) { continue; }
                            SearchOption recursiveScan = SearchOption.TopDirectoryOnly;
                            if (fileAttr.recursiveScan) { recursiveScan = SearchOption.AllDirectories; }
                            allFiles = GetAllFilesFromFolder(filePath, recursiveScan, fileAttr.fileName);
                            //try { allFiles = Directory.GetFiles(filePath, fileAttr.fileName, recursiveScan); }
                            //catch (System.IO.DirectoryNotFoundException exDNF) { total.Logger.Debug(exDNF.Message); continue; }
                            //catch (System.UnauthorizedAccessException exUAE) { total.Logger.Debug(exUAE.Message); continue; }
                            //catch (System.ArgumentException exAE) { total.Logger.Debug(exAE.Message.TrimEnd('.') + " \"" + filePath + "\""); continue; }
                            // ----------------------------------------------------------------------------------------------------
                            //   Save file info details in the INF structure so it can be used by other functions
                            // ----------------------------------------------------------------------------------------------------
                            INF.filePath = filePath;
                            // ----------------------------------------------------------------------------------------------------
                            //   Now for all files found in this path;
                            //   - skip if the exclude Regex matches
                            //   - skip if its age does not match (LastWriteTime)
                            //   - skip if file is hidden and processHiddenFiles is false
                            //   - skip if action is compact and file is already compacted
                            // ----------------------------------------------------------------------------------------------------
                            DateTime fileDateCheck = DateTime.Now.AddDays(Convert.ToDouble(fileAttr.fileAge)*-1);
                            foreach (string fcuFile in allFiles)
                            {
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
                                //   Now execute the requested action
                                // ------------------------------------------------------------------------------------------------
                                switch (fileAttr.actionName.ToLower())
                                {
                                    case "compact":   fcu.CompactFilesInList(fcuFiles, fileAttr); break;
                                    case "delete":    fcu.DeleteFilesInList(fcuFiles, fileAttr); break;
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
                            if (folderAttr.deleteEmptyFolders) { fcu.DeleteEmptyFolders(filePath, recursiveScan); }
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
                // ----------------------------------------------------------------------------------------------------------------
                //   Finish the job by showing how much space has been regained
                // ----------------------------------------------------------------------------------------------------------------
                string prfx = "";  if (total.APP.Dryrun) { prfx = " [DRYRUN] - "; }
                total.Logger.Info(prfx + "Total bytes compacted: " + total_bytesCompacted);
                total.Logger.Info(prfx + "Total bytes deleted  : " + total_bytesDeleted);
                total.Logger.Info(prfx + "Total bytes moved    : " + total_bytesMoved);
                total.Logger.Info(prfx + "Total bytes archived : " + total_bytesArchived);
                total.Logger.Info("--------------------------------------------------------------------------------");
            }
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
            //   Now replace know keywords in string
            // --------------------------------------------------------------------------------------------------------------------
            try { Record = Record.Replace("#SCHEDULE#", INF.scheduleName); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #SCHEDULE#"); }
            try { Record = Record.Replace("#PATH#", INF.filePath); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #PATH#"); }
            try { Record = Record.Replace("#FNAME#", INF.fileName); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FNAME#"); }
            try { Record = Record.Replace("#FBNAME#", INF.fileBaseName); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FBNAME#"); }
            try { Record = Record.Replace("#FEXT#", INF.fileExt); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FEXT#"); }
            try { Record = Record.Replace("#FRDIR#", INF.fileRdir[0]); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FRDIR#"); }
            try { Record = Record.Replace("#FSDIR#", INF.fileSdir[0]); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FSDIR#"); }
            try { Record = Record.Replace("#FDIR#", INF.fileSdir[0]); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FDIR#"); }
            for (int i = 0; i <= 9; i++)
            {
                string rplKW = String.Format("#FR{0}DIR#", i);
                try { Record = Record.Replace(rplKW, INF.fileRdir[i]); }
                catch { total.Logger.Warn("Resolve Keyword Exception on: " + rplKW); }
                rplKW = String.Format("#FS{0}DIR#", i);
                try { Record = Record.Replace(rplKW, INF.fileSdir[i]); }
                catch { total.Logger.Warn("Resolve Keyword Exception on: " + rplKW); }
            }
            try { Record = Record.Replace("#FCDATE#", INF.createDate); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FCDATE#"); }
            try { Record = Record.Replace("#FCMONTH#", INF.createMonth); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FCMONTH#"); }
            try { Record = Record.Replace("#FCYEAR#", INF.createYear); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FCYEAR#"); }
            try { Record = Record.Replace("#FMDATE#", INF.modifyDate); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FMDATE#"); }
            try { Record = Record.Replace("#FMMONTH#", INF.modifyMonth); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FMMONTH#"); }
            try { Record = Record.Replace("#FMYEAR#", INF.modifyYear); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FMYEAR#"); }
            try { Record = Record.Replace("#FADATE#", INF.accessDate); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FADATE#"); }
            try { Record = Record.Replace("#FAMONTH#", INF.accessMonth); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FAMONTH#"); }
            try { Record = Record.Replace("#FAYEAR#", INF.accessYear); }
            catch { total.Logger.Warn("Resolve Keyword Exception on: #FAYEAR#"); }
            // --------------------------------------------------------------------------------------------------------------------
            //   Now call the 'generic' Replacekeyword function and return the result
            // --------------------------------------------------------------------------------------------------------------------
            return total.ReplaceKeyword(Record);
        }

    }
}
