// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace stress.codegen.utils
{
    internal static class FileUtils
    {
        public static async Task<long> CopyDirAsync(string srcPath, string destPath, bool onlyIfNewer = true, bool nuke = false, bool recursive = true, int retryCount = 5, int retryWait = 500)
        {
            long size = 0;
            List<Task> copyTasks = new List<Task>();

            bool destExists = Directory.Exists(destPath);

            //if the directory exists and nuke is true
            if (Directory.Exists(destPath) && nuke)
            {
                //delete the directory recursively before starting the copy
                Action deleteDir = () => Directory.Delete(destPath, true);

                //try the delete retrying as specified
                await deleteDir.TryAsync(retryCount, retryWait);
            }

            //if the dest directory doesn't exist create it
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            //copy all the files asynchronously
            foreach (var srcFile in Directory.EnumerateFiles(srcPath))
            {
                var destFile = Path.Combine(destPath, Path.GetFileName(srcFile));

                Func<Task<long>> copyFile = () => CopyFileAsync(srcFile, destFile, onlyIfNewer);

                //try the copy retrying as specified
                Task copyWithRetry = copyFile.TryAsync<long>(retryCount, retryWait).ContinueWith(task => Interlocked.Add(ref size, task.Result));

                copyTasks.Add(copyWithRetry);
            }

            //if recursive is true we need to copy sub directories as well
            if (recursive)
            {
                //copy all the subdirectories asynchronously
                foreach (var subDir in Directory.EnumerateDirectories(srcPath))
                {
                    var destSubDir = Path.Combine(destPath, Path.GetFileName(subDir));

                    Task subDirTask = CopyDirAsync(subDir, destSubDir, onlyIfNewer, nuke, recursive).ContinueWith(task => Interlocked.Add(ref size, task.Result));

                    copyTasks.Add(subDirTask);
                }
            }

            //await all the file and directory copies
            await Task.WhenAll(copyTasks.ToArray());

            return size;
        }

        public static async Task<long> CopyFileAsync(string srcPath, string destPath, bool onlyIfNewer = true)
        {
            long size = 0;

            //only try to copy if the source file exists
            if (File.Exists(srcPath))
            {
                bool skip = false;

                size = new FileInfo(srcPath).Length;

                //if we only are copying newer files and the dest file exists
                if (onlyIfNewer && File.Exists(destPath))
                {
                    //check if the destination is newer
                    //if so set copy to false so we don't copy the file
                    skip = File.GetLastWriteTimeUtc(srcPath) <= File.GetLastWriteTimeUtc(destPath);
                }

                if (!skip)
                {
                    //open the src as readonly we don't need to write
                    using (Stream srcFile = File.Open(srcPath, FileMode.Open, FileAccess.Read))
                    {
                        //open the dest as Create so we alway overwrite the file
                        using (Stream destFile = File.Open(destPath, FileMode.Create, FileAccess.ReadWrite))
                        {
                            //copy async
                            await srcFile.CopyToAsync(destFile);
                        }
                    }
                }
            }

            return size;
        }

        ///// <summary>
        ///// Try to delete 'fileName' catching any exception.  Returns true if successful.   It will delete read-only files.  
        ///// </summary>  
        //public static bool TryDelete(string fileName)
        //{
        //    bool ret = false;

        //    //if the file does not exist return imediately
        //    if (!File.Exists(fileName))
        //    {
        //        return true;
        //    }

        //    try
        //    {
        //        //if the file is read-only try to remove the read-only attribute
        //        FileAttributes attribs = File.GetAttributes(fileName);

        //        if ((attribs & FileAttributes.ReadOnly) != 0)
        //        {
        //            attribs &= ~FileAttributes.ReadOnly;
        //            File.SetAttributes(fileName, attribs);
        //        }

        //        //delete the file
        //        File.Delete(fileName);

        //        //if no exceptions were thrown the file was deleted
        //        ret = true;
        //    }
        //    //if exceptions were thown the we haven't set the return value to true 
        //    //so we can swallow the exception and return
        //    catch {}

        //    return ret;
        //}

        ///// <summary>
        ///// Delete works much like File.Delete, except that it will succeed if the
        ///// archiveFile does not exist, and will rename the archiveFile so that even if the archiveFile 
        ///// is locked the original archiveFile variable will be made available.  
        ///// 
        ///// It renames the  archiveFile with a '[num].deleting'.  These files might be left 
        ///// behind.  
        ///// 
        ///// It returns true if it was completely successful.  If there is a *.deleting
        ///// archiveFile left behind, it returns false. 
        ///// </summary>
        ///// <param variable="fileName">The variable of the archiveFile to delete</param>
        //public static bool ForceDelete(string path)
        //{
        //    //if the path is a directory force delete the directory
        //    if (Directory.Exists(path))
        //    {
        //        //return true if no files were left behind
        //        return ForceDeleteDirectory(path) == 0;
        //    }

        //    //if the path is not a directory and no file exists return true
        //    if (!File.Exists(path))
        //    {
        //        return true;
        //    }

        //    // First move the archiveFile out of the way, so that even if it is locked
        //    // The original archiveFile is still gone.  
        //    string fileToDelete = path;
        //    bool tryToDeleteOtherFiles = true;
        //    if (!fileToDelete.EndsWith(".deleting", StringComparison.OrdinalIgnoreCase))
        //    {
        //        tryToDeleteOtherFiles = false;
        //        int i = 0;
        //        for (i = 0; ; i++)
        //        {
        //            fileToDelete = path + "." + i.ToString() + ".deleting";
        //            if (!File.Exists(fileToDelete))
        //                break;
        //            tryToDeleteOtherFiles = true;
        //        }
        //        try
        //        {
        //            File.Move(path, fileToDelete);
        //        }
        //        catch (Exception)
        //        {
        //            fileToDelete = path;
        //        }
        //    }
        //    bool ret = TryDelete(fileToDelete);
        //    if (tryToDeleteOtherFiles)
        //    {
        //        // delete any old *.deleting files that may have been left around 
        //        string deletePattern = Path.GetFileName(path) + @".*.deleting";
        //        foreach (string deleteingFile in Directory.GetFiles(Path.GetDirectoryName(path), deletePattern))
        //            TryDelete(deleteingFile);
        //    }
        //    return ret;
        //}


        ///// <summary>
        ///// Clean is sort of a 'safe' recursive delete of a directory.  It either deletes the
        ///// files or moves them to '*.deleting' names.  It deletes directories that are completely
        ///// empty.  Thus it will do a recursive delete when that is possible.  There will only 
        ///// be *.deleting files after this returns.  It returns the number of files and directories
        ///// that could not be deleted.  
        ///// </summary>
        //private static int ForceDeleteDirectory(string directory)
        //{
        //    int ret = 0;

        //    //if the directory doesn't exist return immediately
        //    if (!Directory.Exists(directory))
        //    {
        //        return ret;
        //    }

        //    //force delete all the files
        //    foreach (string file in Directory.GetFiles(directory))
        //    {
        //        if (!ForceDelete(file))
        //        {
        //            ret++;
        //        }
        //    }

        //    foreach (string subDir in Directory.GetDirectories(directory))
        //    {
        //        ret += ForceDeleteDirectory(subDir);
        //    }

        //    if (ret == 0)
        //    {
        //        try
        //        {
        //            Directory.Delete(directory, true);
        //        }
        //        catch
        //        {
        //            ret++;
        //        }
        //    }
        //    //if there were files left behind
        //    //  increment the number of files/directories left behind
        //    //  rename the directory to *.deleting
        //    else
        //    {
        //        ret++;

        //        try
        //        {
        //            Directory.Move(directory, directory + ".deleting");
        //        }
        //        catch { }
        //    }
        //    return ret;
        //}

        //private static bool ForceDeleteFile(string path)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
