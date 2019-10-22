using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Total.Util;

namespace Total.WinFCU
{
    public partial class fcu
    {
        // ------------------------------------------------------------------------------------------------------------------------
        //   Delete all folders in the give path
        // ------------------------------------------------------------------------------------------------------------------------
        public static void DeleteEmptyFolders(string scanPath, Regex excludeFolder, SearchOption recursiveScan)
        {
            string[] allFolders = null;
            total.Logger.Info("Removing empty folders in " + scanPath);
            // --------------------------------------------------------------------------------------------------------------------
            //    Create a list of available folders
            // --------------------------------------------------------------------------------------------------------------------
            try { allFolders = Directory.GetDirectories(scanPath, "*", recursiveScan); }
            catch (PathTooLongException exPTL) { total.Logger.Debug(exPTL.Message + " - " + scanPath); return; }
            catch (DirectoryNotFoundException exDNF) { total.Logger.Debug(exDNF.Message); return; }
            catch (UnauthorizedAccessException exUAE) { total.Logger.Debug(exUAE.Message); return; }
            catch (ArgumentException exAE) { total.Logger.Debug(exAE.Message.TrimEnd('.') + " \"" + scanPath + "\""); return; }
            // --------------------------------------------------------------------------------------------------------------------
            //   Process the list of available folders (sort the list and start with the longest path name)
            // --------------------------------------------------------------------------------------------------------------------
            Array.Sort(allFolders, (x, y) => y.Length.CompareTo(x.Length));
            foreach (string pathName in allFolders)
            {
                if (excludeFolder.Match(pathName).Success) { total.Logger.Info("Excluding path: " + pathName); continue; }
                bool dirDeleted = true;
                DirectoryInfo dirInfo = null;
                try { dirInfo = new DirectoryInfo(pathName); }
                catch (PathTooLongException exPTL) { total.Logger.Debug(exPTL.Message + " - " + pathName); return; }
                if (total.APP.Dryrun) { total.Logger.Debug(" [DRYRUN] - would remove folder: " + pathName); }
                else
                {
                    try { dirInfo.Delete(false); }
                    catch (IOException) { dirDeleted = false; }
                    if (dirDeleted) { total.Logger.Debug("Removed folder " + pathName); }
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Delete all files in the specified FileInfo list (verify that file still exists! might be removed by external source)
        // ------------------------------------------------------------------------------------------------------------------------
        public static void DeleteFilesInList(List<FileInfo> fileList)
        {
            foreach (FileInfo file in fileList)
            {
                string fullName = file.FullName;
                if (!File.Exists(fullName)) { continue; }
                long fileSize   = file.Length;
                if (total.APP.Dryrun) { total.Logger.Debug(" [DRYRUN] - would delete file: " + fullName + "  (" + fileSize + ")"); }
                else
                {
                    total.Logger.Debug("  delete file: " + fullName + "  (" + fileSize + ")");
                    try
                    {
                        file.IsReadOnly = false;
                        file.Delete();
                        total.Logger.Info(" deleted file: " + fullName + "  (" + fileSize + ")");
                        folder_bytesDeleted += fileSize;
                    }
                    catch (UnauthorizedAccessException ex) { total.Logger.Warn(fullName + " - " + ex.Message); }
                    catch (IOException ex)                 { total.Logger.Warn(ex.Message); }
                    catch (Exception ex)                   { total.Logger.Warn(fullName + " - " + ex.Message); }
                }
            }
            total_bytesDeleted += folder_bytesDeleted;
            if (total.APP.Dryrun) { total.Logger.Info(" [DRYRUN] - would have deleted " + folder_bytesDeleted + " bytes in folder " + INF.filePath); }
            else { total.Logger.Info("Deleted " + folder_bytesDeleted + " bytes in folder " + INF.filePath); }
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Compact all files in the specified FileInfo list (verify that file still exists! might be removed by external source)
        // ------------------------------------------------------------------------------------------------------------------------
        public static void CompactFilesInList(List<FileInfo> fileList)
        {
            foreach (FileInfo file in fileList)
            {
                if (!File.Exists(file.FullName)) { continue; }
                // ----------------------------------------------------------------------------------------------------------------
                //   No use to comress when allready comressed.
                // ----------------------------------------------------------------------------------------------------------------
                if ((File.GetAttributes(file.FullName) & FileAttributes.Compressed) == FileAttributes.Compressed) { continue; }
                // ----------------------------------------------------------------------------------------------------------------
                //   Compress the specified file, the function will return the new filesize.
                // ----------------------------------------------------------------------------------------------------------------
                long fileLengthBefore = file.Length;
                long fileLengthAfter = 0;
                if (total.APP.Dryrun) { total.Logger.Debug(" [DRYRUN] - would compress file: " + file.FullName + "  (" + file.Length + ")"); }
                else
                {
                    total.Logger.Debug(" compact file: " + file.FullName + "  (" + file.Length + ")");
                    try { fileLengthAfter = total.CompactFile(file.FullName); }
                    catch (Exception ex) { total.Logger.Warn(file.FullName + " - " + ex.Message); continue; }
                    total.Logger.Info("compacted file: " + file.FullName + "  (" + fileLengthBefore + "/" + fileLengthAfter + ")");
                }
                folder_bytesCompacted += (fileLengthBefore - fileLengthAfter);
                folder_CompationRatio += (fileLengthBefore / fileLengthAfter);
            }
            total_bytesZipped += folder_bytesZipped;
            if (total.APP.Dryrun) { total.Logger.Info(" [DRYRUN] - would have compressed " + folder_bytesCompacted + " bytes in folder " + INF.filePath); }
            else { total.Logger.Info("Compressed " + folder_bytesCompacted + " bytes in folder " + INF.filePath); }
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Move all files in the specified FileInfo list to the specified target
        // ------------------------------------------------------------------------------------------------------------------------
        public static void MoveFilesInList(List<FileInfo> fileList, scanAttributes fnAttr)
        {
            foreach (FileInfo file in fileList)
            {
                if (!File.Exists(file.FullName)) { continue; }
                // --------------------------------------------------------------------------------------------------------------------
                //   Collect file info of the file to move
                // --------------------------------------------------------------------------------------------------------------------
                SetKeywordValues(file);
                // --------------------------------------------------------------------------------------------------------------------
                //   Determine the target drive, path and name. Create the destination folder in case it does not exist
                //   If the target is not an absolute path, it will be treated as relative to the source
                //   If the target has no filename specified, the source filename will be used
                // --------------------------------------------------------------------------------------------------------------------
                string sourceFile = file.FullName;
                string targetFile = fcu.ReplaceKeyword(fnAttr.actionTarget);
                if (targetFile.Length == 0) { total.Logger.Error("No target specified for " + sourceFile + " - skipping move action"); return; }
                if (!Path.IsPathRooted(targetFile)) { targetFile = Path.Combine(file.DirectoryName, targetFile); }
                if (Path.GetFileName(targetFile).Length == 0) { targetFile = Path.Combine(targetFile, file.Name); }
                if (total.APP.Dryrun)
                {
                    total.Logger.Debug(" [DRYRUN] - would move file: " + sourceFile + " to " + targetFile + "  (" + file.Length + ")");
                    folder_bytesMoved += file.Length;
                    continue;
                }
                string targetPath = Path.GetDirectoryName(targetFile);
                if (!Directory.Exists(targetPath))
                {
                    try { Directory.CreateDirectory(targetPath); }
                    catch (Exception ex) { total.Logger.Fatal("Failed to create directory for " + targetFile, ex); return; }
                }
                // --------------------------------------------------------------------------------------------------------------------
                //   Move the file to the specified target
                // --------------------------------------------------------------------------------------------------------------------
                if (File.Exists(targetFile))
                {
                    if (fnAttr.forceOverWrite) { File.Delete(targetFile); }
                    else
                    {
                        bool tfExists = true; int tfNum = 0;
                        string tfExt  = Path.GetExtension(targetFile);
                        string tfName = Path.Combine(Path.GetDirectoryName(targetFile), Path.GetFileNameWithoutExtension(targetFile));
                        while (tfExists)
                        {
                            tfNum += 1;
                            targetFile = String.Format("{0}-{1:0000}{2}", tfName, tfNum, tfExt);
                            tfExists = File.Exists(targetFile);
                        }
                    }
                }
                total.Logger.Debug("  move file: " + sourceFile + " to " + targetFile + "  (" + file.Length + ")");
                try { File.Move(sourceFile, targetFile); }
                catch (DirectoryNotFoundException) { Directory.CreateDirectory(Path.GetDirectoryName(targetFile)); file.MoveTo(targetFile); }
                catch (Exception ex) { total.Logger.Debug(sourceFile + " - " + ex.Message); continue; }
                total.Logger.Info(" moved file: " + sourceFile + "  to: " + targetFile + "  (" + file.Length + ")");
                folder_bytesMoved += file.Length;
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   All files moved, update the counter, show what has been achieved and return
            // --------------------------------------------------------------------------------------------------------------------
            total_bytesMoved += folder_bytesMoved;
            if (total.APP.Dryrun) { total.Logger.Info(" [DRYRUN] - would have moved " + folder_bytesMoved + " bytes from folder " + INF.filePath); }
            else { total.Logger.Info("Moved " + folder_bytesMoved + " bytes from folder " + INF.filePath); }
            // --------------------------------------------------------------------------------------------------------------------
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Archive all files in the specified FileInfo list into the specified archive
        // ------------------------------------------------------------------------------------------------------------------------
        public static void ArchiveFilesInList(List<FileInfo> fileList, scanAttributes fnAttr)
        {
            // --------------------------------------------------------------------------------------------------------------------
            //   Convert the input file list to a sorted list with the archive target as key, the value part of the sorted list
            //   holds all filenames for the archive target.
            // --------------------------------------------------------------------------------------------------------------------
            SortedList<string, List<string>> archiveSet = new SortedList<string, List<string>>();
            // --------------------------------------------------------------------------------------------------------------------
            //  Process all files in the list
            // --------------------------------------------------------------------------------------------------------------------
            foreach (FileInfo inFile in fileList)
            {
                // ----------------------------------------------------------------------------------------------------------------
                //  In case of a relative archive location, the file determines the location/name of the archive
                // ----------------------------------------------------------------------------------------------------------------
                SetKeywordValues(inFile);
                string archive = Path.GetFullPath(Path.Combine(inFile.DirectoryName, fcu.ReplaceKeyword(fnAttr.actionTarget)));
                // ----------------------------------------------------------------------------------------------------------------
                //   Add the current file and its archive to the dictionary using the archivename as key
                // ----------------------------------------------------------------------------------------------------------------
                try
                {
                    List<string> archiveList = archiveSet[archive];
                    archiveSet[archive].Add(inFile.FullName);
                }
                catch
                {
                    total.Logger.Debug("Collecting files for archive \"" + archive + "\"");
                    List<string> archiveList = new List<string>() { inFile.FullName };
                    archiveSet.Add(archive, archiveList);
                }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   For each archiveTarget, start an archive action for the appropriate files
            // --------------------------------------------------------------------------------------------------------------------
            foreach (string archiveTarget in archiveSet.Keys)
            {
                List<string> archiveList = archiveSet[archiveTarget];
                // ----------------------------------------------------------------------------------------------------------------
                //  Archiver takes care of creating batches (in case too many files must be archived) etc.
                // ----------------------------------------------------------------------------------------------------------------
                if (!total.APP.Dryrun)
                {
                    folder_bytesArchived = folder_bytesDeleted = 0;
                    // ------------------------------------------------------------------------------------------------------------
                    //  Create the destination folder in case it does not exist
                    // -------------------------------------------------------------------------------------------------------------
                    string targetPath = Path.GetDirectoryName(archiveTarget);
                    if (!Directory.Exists(targetPath))
                    {
                        try { Directory.CreateDirectory(targetPath); }
                        catch (Exception ex) { total.Logger.Fatal("Failed to create directory for \"" + archiveTarget + "\"", ex); return; }
                    }
                    // ------------------------------------------------------------------------------------------------------------
                    //  Process the files for the given target - target by target
                    // -------------------------------------------------------------------------------------------------------------
                    List<total.ARC> archiveResult = total.CompressFiles(archiveList.ToArray(), archiveTarget, CompressionLevel.Optimal, 100, true, fnAttr.archivePath);
                    foreach (var entry in archiveResult)
                    {
                        folder_bytesDeleted  += entry.orgFileSize;
                        folder_bytesArchived += entry.cmpFileSize;
                    }
                }
                archiveList.Clear();
                decimal folder_ZipRatio = folder_bytesDeleted > 0 ? Math.Round((decimal)100 * folder_bytesArchived / folder_bytesDeleted, 1) : -1;
                total_bytesArchived += folder_bytesArchived;
                total_bytesDeleted  += folder_bytesDeleted;
                total.Logger.Debug("Archive results (D/A/R) for folder \"" + INF.filePath + "\" - " + folder_bytesDeleted + "/" + folder_bytesArchived + "/" + folder_ZipRatio);
            }
            archiveSet.Clear();
        }

    // ----------------------------------------------------------------------------------------------------------------------------
    }
}
