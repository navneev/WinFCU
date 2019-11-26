using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Total.Util
{
    // ------------------------------------------------------------------------------------------------------------------------
    //   Compact the specified file and return its new size. When already compressed just return its size
    // ------------------------------------------------------------------------------------------------------------------------
    internal static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetCompressedFileSize", CharSet = CharSet.Unicode)]
        static extern uint GetCompressedFileSize(string lpFileName, out uint lpFileSizeHigh);
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "DeviceIoControl", CharSet = CharSet.Unicode)]
        static extern int DeviceIoControl(SafeHandle hDevice, int dwIoControlCode, ref short lpInBuffer, int nInBufferSize,
                                          IntPtr lpOutBuffer, int nOutBufferSize, ref int lpBytesReturned, IntPtr lpOverlapped);
    }

    public partial class total
    {
        private static uint GetCompressedFileSize(string file, out uint fileSizeHigh)
        {
            throw new NotImplementedException();
        }

        private static int DeviceIoControl(SafeFileHandle safeFileHandle, int fSCTL_SET_COMPRESSION, ref short cOMPRESSION_FORMAT_DEFAULT, int v1, IntPtr zero1, int v2, ref int lpBytesReturned, IntPtr zero2)
        {
            throw new NotImplementedException();
        }

        // ------------------------------------------------------------------------------------------------------------------------
        public static long CompactFile(string file)
        {
            // --------------------------------------------------------------------------------------------------------------------
            //   No use to compress when allready comressed.
            // --------------------------------------------------------------------------------------------------------------------
            if ((File.GetAttributes(file) & FileAttributes.Compressed) != FileAttributes.Compressed)
            {
                // ----------------------------------------------------------------------------------------------------------------
                //   Try to compress the file
                // ----------------------------------------------------------------------------------------------------------------
                int lpBytesReturned = 0;
                int FSCTL_SET_COMPRESSION = 0x9C040;
                short COMPRESSION_FORMAT_DEFAULT = 1;
                try
                {
                    FileStream _cf = File.Open(file, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None);
                    int result = DeviceIoControl(_cf.SafeFileHandle, FSCTL_SET_COMPRESSION, ref COMPRESSION_FORMAT_DEFAULT, 2, IntPtr.Zero, 0, ref lpBytesReturned, IntPtr.Zero);
                    _cf.Close();
                    _cf.Dispose();
                }
                catch (Exception ex) { Logger.Fatal("Compaction of '" + file + "' failed! - (" + ex.Message + ")"); }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   File has been compressed, return its new size
            // --------------------------------------------------------------------------------------------------------------------
            uint fileSizeHigh = 0;
            uint fileSizeLow = 0;
            try { fileSizeLow = GetCompressedFileSize(file, out fileSizeHigh); }
            catch (Exception ex) { Logger.Error(ex.Message); }
            return Convert.ToInt64(((ulong)fileSizeHigh << 32) + fileSizeLow);
        }

        // ------------------------------------------------------------------------------------------------------------------------
        //   Archive (zip) the specified files into the specified archive using IonicZIP - obsolete
        // ------------------------------------------------------------------------------------------------------------------------
/*      public static List<ARC> ArchiveFiles(string[] inputFiles, string archiveName, string archiveTemp = null,
                                             string dirPathInArchive = null, bool preserveDirHierarchy = true,
                                             bool deleteFilesWhenArchived = false, int compressionLevel = 6, int batchSize = 100)
        {
            ///<summary>
            /// Archive files into specified archive
            ///</summary>
            ///<param name="inputFiles">An array of files (fullname) which will be archived.</param>
            ///<param name="archiveName">The fullname of the zip archive to which the files will be archived.</param>
            ///<param name="archiveTemp">Name of the folder used for temp storage.</param>
            ///<param name="dirPathInArchive">Specifies a directory path to use to override any path in the fileName. This path may, or may not, correspond to a real directory in the current filesystem. If the files within the zip are later extracted, this is the path used for the extracted file. Passing null will use the path on the fileName, if any. Passing the empty string ("") will insert the item at the root path within the archive.</param>
            ///<param name="preserveDirHierarchy">When false no filepath will be stored and this can result in an exception because of a duplicate entry name, while calling this method with preserveDirHierarchy = true will result in the full direcory paths being included in the entries added to the ZipFile.</param>
            ///<param name="deleteFilesWhenArchived">Delete the source file when successfully archived.</param>
            ///<param name="compressionLevel">A number specifying the levelof compression (0=None...9=Best, 6=Default).</param>
            ///<param name="batchSize">Maximum number of files to archive in a run. This number cis limited when 50% of the temp space is used by the archiver.</param>
            // --------------------------------------------------------------------------------------------------------------------
            //   Check the specified archiveName, if the file already exists we better check whether it is a valid and
            //   healthy archive. When the zip is initialized, set some options (temp folder, compression, etc)
            // --------------------------------------------------------------------------------------------------------------------
            Ionic.Zlib.CompressionLevel[] zipCompressionLevel = { Ionic.Zlib.CompressionLevel.Level0, Ionic.Zlib.CompressionLevel.Level1, Ionic.Zlib.CompressionLevel.Level2,
                                                                  Ionic.Zlib.CompressionLevel.Level3, Ionic.Zlib.CompressionLevel.Level4, Ionic.Zlib.CompressionLevel.Level5,
                                                                  Ionic.Zlib.CompressionLevel.Level6, Ionic.Zlib.CompressionLevel.Level7, Ionic.Zlib.CompressionLevel.Level8,
                                                                  Ionic.Zlib.CompressionLevel.Level9 };
            Ionic.Zip.ZipFile zip = null;
            if (!File.Exists(archiveName)) { zip = new Ionic.Zip.ZipFile(archiveName); }
            else
            {
                if (!Ionic.Zip.ZipFile.IsZipFile(archiveName)) { Logger.Error("File '" + archiveName + "' already exists but is not a valid ZIP archive"); Environment.Exit(1); }
                if (!Ionic.Zip.ZipFile.CheckZip(archiveName))  { Logger.Error("File '" + archiveName + "' already exists but is a corrupted ZIP archive"); Environment.Exit(1); }
                zip = Ionic.Zip.ZipFile.Read(archiveName);
            }
            zip.CompressionLevel = zipCompressionLevel[compressionLevel];
            zip.TempFileFolder = archiveTemp;
            zip.ZipErrorAction = ZipErrorAction.Throw;
            // --------------------------------------------------------------------------------------------------------------------
            //    The archive is ready to receive.......
            // --------------------------------------------------------------------------------------------------------------------
            Logger.Info("Archiving " + inputFiles.Length + " files to: " + archiveName);
            List<ARC> archiveResult = new List<ARC>();
            if (dirPathInArchive == null) {  if (!preserveDirHierarchy) { dirPathInArchive = ""; } }
            // --------------------------------------------------------------------------------------------------------------------
            //   Calculate the ammount of available space. Use 45% for temp storage and 50% for final storage.
            // --------------------------------------------------------------------------------------------------------------------
            long[] targetSpace = SaveNativeMethods.GetFreeDiskSpace(archiveName);
            long tmpSpace = targetSpace[0] / 100 * 45;
            long maxSpace = targetSpace[0] / 2;
            long fileSpace = 0;
            int fileCount = 0;
            int batchNo = 1;
            // --------------------------------------------------------------------------------------------------------------------
            //   If needed create a random temp folder on the target drive which can be used by the archiver
            // --------------------------------------------------------------------------------------------------------------------
            bool createdTemp = false;
            if (archiveTemp == null) { archiveTemp = Path.Combine(Path.GetPathRoot(archiveName), "$ZIP"+(DateTime.Now.Ticks).ToString("X")); }
            if (!Directory.Exists(archiveTemp)) { Directory.CreateDirectory(archiveTemp); createdTemp = true; }
            // --------------------------------------------------------------------------------------------------------------------
            //   Create archive batches to prevent overloading the disk.
            // --------------------------------------------------------------------------------------------------------------------
            foreach (string inputFile in inputFiles)
            {
                FileInfo fi = new FileInfo(inputFile);
                fileSpace += fi.Length;
                fileCount += 1;
                // ----------------------------------------------------------------------------------------------------------------
                //   A batch is limited by 2 things: its size (no more than batchSize files in a batch) and by the total fileSize
                //   The total size of the files in a batch is not to exceed maxSpace. Keep adding files until 1 of these
                //   conditions is met. and when met, save the batch and initialize the couters
                // ----------------------------------------------------------------------------------------------------------------
                if ((fileSpace >= maxSpace) || (fileCount > batchSize))
                {
                    fileCount -= 1;
                    Logger.Debug("  Saving batch no: " + batchNo + "  -  " + fileCount + " files (" + fileSpace + " bytes)");
                    try { zip.Save(); }
                    catch (Exception ex) { Logger.Debug(ex.Message); }
                    fileCount = 1;
                    fileSpace = fi.Length;
                    batchNo += 1;
                }
                // ----------------------------------------------------------------------------------------------------------------
                //  Process the file in the pipeline and return for more
                // ----------------------------------------------------------------------------------------------------------------
                if (zip.ContainsEntry(inputFile))
                {
                    Logger.Debug("  Updating " + inputFile + " in archive");
                    try { zip.UpdateFile(inputFile, dirPathInArchive); }
                    catch (Exception ex) { Logger.Debug(ex.Message); }
                }
                else
                {
                    Logger.Debug("  Adding " + inputFile + " to archive");
                    try { zip.AddFile(inputFile, dirPathInArchive); }
                    catch (Exception ex) { Logger.Debug(ex.Message); }
                }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   All files have been processed, but there can still be some waiting to be processes. Let's do so now
            // --------------------------------------------------------------------------------------------------------------------
            if (fileCount > 0)
            {
                Logger.Debug("  Saving batch no: " + batchNo + "  -  " + fileCount + " files (" + fileSpace + " bytes)");
                try { zip.Save(); }
                catch (Exception ex) { Logger.Debug(ex.Message); }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Verify the archive by comparing zipped and source file. If a file is successfully archived and
            //   deleteFilesWhenArchived has been set,then delete the source file.
            // --------------------------------------------------------------------------------------------------------------------
            Logger.Debug("  verifying archive '" + archiveName +"'");
            foreach (string inputFile in inputFiles)
            {
                // ------------------------------------------------------------------------------------------------------------
                //   Archived file format might have changed by the usage of preserveDirHierarchy and/or dirPathInArchive
                // ------------------------------------------------------------------------------------------------------------
                string verifyFile = inputFile;
                if (!preserveDirHierarchy) { verifyFile = Path.GetFileName(inputFile); }
                if (dirPathInArchive != null) { verifyFile = Path.Combine(dirPathInArchive, verifyFile); }
                // ------------------------------------------------------------------------------------------------------------
                //    1st check - is the file archived? If not continue with next file
                // ------------------------------------------------------------------------------------------------------------
                if (!zip.ContainsEntry(verifyFile)) { Logger.Warn(inputFile + " has not been archived!"); continue; }
                // ------------------------------------------------------------------------------------------------------------
                //    2nd check - a file of this name is archived, but is it this file? Check attributes
                // ------------------------------------------------------------------------------------------------------------
                ZipEntry zeInfo = zip[verifyFile];
                FileInfo ifInfo = new FileInfo(inputFile);
                if (zeInfo.CreationTime.CompareTo(ifInfo.CreationTimeUtc) != 0) { Logger.Error("C - Previous version of '" + inputFile + "' found in archive?"); continue; }
                if (zeInfo.AccessedTime.CompareTo(ifInfo.LastAccessTimeUtc) != 0) { Logger.Error("A - Previous version of '" + inputFile + "' found in archive?"); continue; }
                if (zeInfo.UncompressedSize != ifInfo.Length) { Console.WriteLine("U/O: " + zeInfo.UncompressedSize + "/" + ifInfo.Length); Logger.Error("S - Previous version of '" + inputFile + "' found in archive?"); continue; }
                ARC arcInfo;
                arcInfo.Filename     = inputFile;
                arcInfo.orgFileSize  = ifInfo.Length;
                arcInfo.cmpFileSize  = zeInfo.CompressedSize;
                arcInfo.bytesDeleted = Convert.ToInt32(arcInfo.orgFileSize - arcInfo.cmpFileSize);
                arcInfo.cmpRatio     = Convert.ToInt32(zeInfo.CompressionRatio);
                Logger.Debug(" archiver details: " + arcInfo.Filename + "\t " + arcInfo.orgFileSize + "/" + arcInfo.cmpFileSize + "/" + arcInfo.cmpRatio);
                archiveResult.Add(arcInfo);
                if (deleteFilesWhenArchived)
                {
                    Logger.Debug("Archived file OK, deleting '" + inputFile + "'");
                    ifInfo.IsReadOnly = false;
                    try { ifInfo.Delete(); }
                    catch (Exception ex) { Logger.Warn(ex.Message); continue; }
                }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Remove the random temp foder on the target drive which has been used by the archiver
            // --------------------------------------------------------------------------------------------------------------------
            if (createdTemp & Directory.Exists(archiveTemp)) { Directory.Delete(archiveTemp, true); }
            zip.Dispose();
            return archiveResult;
        }
*/

        // ------------------------------------------------------------------------------------------------------------------------
        //   Creates a zipped (or compressed) archive file from one or more specified files or folders using System.IO.Compression.
        // ------------------------------------------------------------------------------------------------------------------------
        public static List<ARC> CompressFiles(string[] files, string archive, CompressionLevel compressionLevel = CompressionLevel.Optimal,
                                              int batchSize = 100, bool deleteFilesWhenArchived = false, string archivePath = "None")
        {
            ///<summary>
            /// Compress files into specified compressed folder
            ///</summary>
            ///<param name="files">An array of files (fullname) which will be archived.</param>
            ///<param name="archive">The (full) name of the zip archive to which the files will be archived.</param>
            ///<param name="compressionLevel">A number specifying the levelof compression (0=Optimal [default], 1=Fastest, 2=NoCompression).</param>
            ///<param name="batchSize">Maximum number of files to archive per compress action.</param>
            ///<param name="deleteFilesWhenArchived">Delete the source file when successfully archived.</param>
            ///<param name="archivePath">Use relative, absolute or no path in archive</param>
            // --------------------------------------------------------------------------------------------------------------------
            //   Local 'static' data
            // --------------------------------------------------------------------------------------------------------------------
            Dictionary<string, string> archivedFiles = new Dictionary<string, string>();
            List<ARC> archiveResult = new List<ARC>();
            ARC arcInfo = new ARC();
            ZipArchive zipArchive;
            // --------------------------------------------------------------------------------------------------------------------
            //   Initialize local variables (compression, etc)
            // --------------------------------------------------------------------------------------------------------------------
            string archiveDirName = Path.GetDirectoryName(archive);
            string achiveFileName = Path.GetFileName(archive);
            // --------------------------------------------------------------------------------------------------------------------
            //   Check the specified archive path and name and join then into a valid path specification
            // --------------------------------------------------------------------------------------------------------------------
            if (archiveDirName == null) { Logger.Error("The destination path " + archive + " does not contain a valid archive file name"); Environment.Exit(1); }
            if (archiveDirName == string.Empty) { archiveDirName = "."; }
            string[] archiveList = total.ResolvePath(archiveDirName);
            if (archiveList.Length < 1) { Logger.Error("The archive file path " + archive + " specified is not resolving to an existing file system path. Provide an existing path where the archive file has to be created."); Environment.Exit(1); }
            if (archiveList.Length > 1) { Logger.Error("The archive file path " + archive + " specified is resolving to multiple file system paths. Provide a unique path where the archive file has to be created."); Environment.Exit(1); }
            string archiveRoot = archiveList[0];
            archive = Path.Combine(archiveRoot, achiveFileName);
            // --------------------------------------------------------------------------------------------------------------------
            //   A valid path specification in this case has a .zip extension! (Compressed Folder). Add it when missing!
            //   If the compressed folder already exists, use update to add files to the 'archive'
            // --------------------------------------------------------------------------------------------------------------------
            string pathExtension = Path.GetExtension(archive);
            if ((pathExtension == string.Empty) || (!pathExtension.Equals(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                Logger.Warn("The destination path " + archive + " does not have the correct .zip extension, appending it now");
                achiveFileName += ".zip";
                archive = Path.Combine(archiveRoot, achiveFileName);
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Prepare the archiver process. Only archive 'batchSize' files in one run. Store the entry names in an List
            //   so we can verify (and if wanted delete) then after the run is complete
            //   If the archive already exists and opening the archive fails, than archive to a temp archive (tmp-'originalname')
            // --------------------------------------------------------------------------------------------------------------------
            Logger.Info("Compressing " + files.Length + " files into: " + archive);
            try { zipArchive = ZipFile.Open(archive, ZipArchiveMode.Update); }
            catch (Exception ex)
            {
                Logger.Error("Error opening archive " + archive + " - " + ex.Message);
                archive = Path.Combine(archiveRoot, "tmp-" + achiveFileName);
                Logger.Warn(" archiving to " + archive);
                zipArchive = ZipFile.Open(archive, ZipArchiveMode.Update);
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Archive the requested files using the specified compressionlevel, internal archivepath, etc
            //   Per file verify whether it (still) exists
            // --------------------------------------------------------------------------------------------------------------------
            int fileCounter = 0; int batchNumber = 1;
            foreach (string file in files)
            {
                FileInfo fileInfo = new FileInfo(file);
                if (!File.Exists(fileInfo.FullName)) { continue; }
                string entryName = Path.GetFileName(file);
                //  Verify and set the internal archive path
                switch (archivePath.ToLower())
                {
                    case "none":      entryName = Path.GetFileName(file); break;
                    case "absolute":  entryName = Path.GetFullPath(file).Replace(Path.GetPathRoot(file), "").TrimStart('\\'); break;
                    case "relative":  entryName = Path.GetFullPath(file).Replace(archiveRoot, "").TrimStart('\\'); break;
                    default: Logger.Error("The archivepath type " + archivePath + " is invalid. Use either Absolute, Relative or None."); Environment.Exit(1); break;
                }
                // Archive the file
                try
                {
                    Logger.Debug("  Compressing: " + file);
                    zipArchive.CreateEntryFromFile(file, entryName, compressionLevel);
                    archivedFiles.Add(entryName, file);
                    fileCounter += 1;
                    // --------------------------------------------------------------------------------------------------------
                    //   Once the max number of files have been compressed, reset the counters, close and reopen the archive
                    // --------------------------------------------------------------------------------------------------------
                    if (fileCounter >= batchSize)
                    {
                        Logger.Debug(" saving batch no: " + batchNumber + "  -  " + fileCounter + " files");
                        fileCounter = 0; batchNumber += 1;
                        zipArchive.Dispose();
                        zipArchive = ZipFile.Open(archive, ZipArchiveMode.Update);
                    }
                }
                catch (Exception ex) { Logger.Warn(ex.Message); }
            }
            Logger.Debug(" saving batch no: " + batchNumber + "  -  " + fileCounter + " files");
            zipArchive.Dispose();
            // ----------------------------------------------------------------------------------------------------------------
            //   Fetch the details of all files archived
            // ----------------------------------------------------------------------------------------------------------------
            try
            {
                ZipArchive checkArchive = ZipFile.OpenRead(archive);
                foreach (KeyValuePair<string, string> kvp in archivedFiles)
                {
                    string entryName = kvp.Key;
                    // --------------------------------------------------------------------------------------------------------
                    //   The archivedFiles list only contains files which have been successfully archived
                    // --------------------------------------------------------------------------------------------------------
                    ZipArchiveEntry archivedEntry = checkArchive.GetEntry(entryName);
                    arcInfo.Filename = archivedEntry.FullName;
                    arcInfo.orgFileSize  = archivedEntry.Length;
                    arcInfo.cmpFileSize  = archivedEntry.CompressedLength;
                    arcInfo.cmpRatio = archivedEntry.Length > 0 ? Math.Round((decimal)100 * archivedEntry.CompressedLength / archivedEntry.Length, 1) : -1;
                    Logger.Info(" archived file: " + arcInfo.Filename + " (" + arcInfo.orgFileSize + ")");
                    archiveResult.Add(arcInfo);
                    // --------------------------------------------------------------------------------------------------------
                    //   Delete source file after successful archive?
                    // --------------------------------------------------------------------------------------------------------
                    if (deleteFilesWhenArchived)
                    {
                        Logger.Debug("  archived file OK, deleting '" + kvp.Value + "'");
                        if (File.Exists(kvp.Value))
                        {
                            try { File.Delete(kvp.Value); }
                            catch (Exception ex) { Logger.Warn(ex.Message); }
                        }
                    }
                }
                checkArchive.Dispose();
            }
            catch (Exception ex) { Logger.Warn(ex.Message); }
            return archiveResult;
        }

    }
}
