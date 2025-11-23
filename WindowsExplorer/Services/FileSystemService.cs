using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WindowsExplorer.Models;

namespace WindowsExplorer.Services
{
    /// <summary>
    /// Service for file system operations.
    /// </summary>
    public class FileSystemService
    {
        /// <summary>
        /// Gets the list of files and folders in the specified directory.
        /// </summary>
        public List<FileSystemItem> GetItems(string path)
        {
            var items = new List<FileSystemItem>();

            try
            {
                // Handle root path (empty string) - return all drives
                if (string.IsNullOrEmpty(path))
                {
                    var drives = DriveInfo.GetDrives()
                        .Where(d => d.IsReady)
                        .OrderBy(d => d.Name)
                        .Select(drive =>
                        {
                            var name = string.IsNullOrWhiteSpace(drive.VolumeLabel) 
                                ? drive.Name.TrimEnd('\\') 
                                : $"{drive.Name.TrimEnd('\\')} ({drive.VolumeLabel})";
                            return new FileSystemItem
                            {
                                Name = name,
                                DisplayName = name,
                                Path = drive.RootDirectory.FullName,
                                IsDirectory = true,
                                Size = 0,
                                LastModified = drive.RootDirectory.LastWriteTime,
                                Type = "Local Disk"
                            };
                        })
                        .ToList();
                    items.AddRange(drives);
                    return items;
                }

                if (!Directory.Exists(path))
                {
                    return items;
                }

                var directoryInfo = new DirectoryInfo(path);

                // Get directories first
                var directories = directoryInfo.GetDirectories()
                    .OrderBy(d => d.Name)
                    .Select(FileSystemItem.FromDirectoryInfo)
                    .ToList();

                // Get files
                var files = directoryInfo.GetFiles()
                    .OrderBy(f => f.Name)
                    .Select(FileSystemItem.FromFileInfo)
                    .ToList();

                items.AddRange(directories);
                items.AddRange(files);
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied - return empty list
            }
            catch (DirectoryNotFoundException)
            {
                // Directory not found - return empty list
            }
            catch (Exception)
            {
                // Other errors - return empty list
            }

            return items;
        }

        /// <summary>
        /// Checks if the specified path exists and is accessible.
        /// </summary>
        public bool PathExists(string path)
        {
            try
            {
                // Empty string represents the root (all drives) - always exists
                if (string.IsNullOrEmpty(path))
                {
                    return true;
                }
                return Directory.Exists(path) || File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the parent directory path.
        /// </summary>
        public string? GetParentPath(string path)
        {
            try
            {
                // Empty string is the root - no parent
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                if (File.Exists(path))
                {
                    return Directory.GetParent(path)?.FullName;
                }
                else if (Directory.Exists(path))
                {
                    var parent = Directory.GetParent(path);
                    // If parent is null, we're at a drive root, so return empty string (PC root)
                    if (parent == null)
                    {
                        return string.Empty;
                    }
                    return parent.FullName;
                }
            }
            catch
            {
                // Return null on error
            }

            return null;
        }

        /// <summary>
        /// Gets the default starting path (PC root showing all drives).
        /// </summary>
        public string GetDefaultPath()
        {
            return string.Empty; // Empty string represents the PC root (all drives)
        }

        /// <summary>
        /// Gets a relative path from basePath to targetPath.
        /// </summary>
        private string GetRelativePath(string basePath, string targetPath)
        {
            try
            {
                if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(targetPath))
                {
                    return targetPath;
                }

                var baseUri = new Uri(basePath.EndsWith("\\") ? basePath : basePath + "\\");
                var targetUri = new Uri(targetPath.EndsWith("\\") ? targetPath : targetPath + "\\");
                var relativeUri = baseUri.MakeRelativeUri(targetUri);
                var relativePath = Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', '\\');
                
                return relativePath;
            }
            catch
            {
                // If we can't compute relative path, return the full path
                return targetPath;
            }
        }

        /// <summary>
        /// Recursively searches for files and folders matching the search term.
        /// Uses lazy enumeration to avoid loading everything into memory.
        /// </summary>
        public IEnumerable<FileSystemItem> SearchRecursive(string rootPath, string searchTerm, Action<int, int>? progressCallback = null)
        {
            if (string.IsNullOrEmpty(rootPath) || string.IsNullOrWhiteSpace(searchTerm))
            {
                System.Diagnostics.Debug.WriteLine($"[Search] SearchRecursive: rootPath='{rootPath}', searchTerm='{searchTerm}' - returning early");
                yield break;
            }

            System.Diagnostics.Debug.WriteLine($"[Search] Starting search in '{rootPath}' for '{searchTerm}'");

            var searchLower = searchTerm.ToLowerInvariant();
            var searchStack = new Stack<string>();
            searchStack.Push(rootPath);
            
            int foldersChecked = 0;
            int filesChecked = 0;
            int lastProgressUpdate = 0;

            while (searchStack.Count > 0)
            {
                var currentPath = searchStack.Pop();

                // Check if directory exists before processing
                if (!Directory.Exists(currentPath))
                {
                    continue;
                }

                DirectoryInfo? directoryInfo = null;
                try
                {
                    directoryInfo = new DirectoryInfo(currentPath);
                }
                catch
                {
                    // Can't access directory, skip it
                    continue;
                }

                // Increment folder count
                foldersChecked++;
                
                // Enumerate files in current directory (more fault-tolerant than GetFiles)
                IEnumerable<string> filePaths = Array.Empty<string>();
                try
                {
                    filePaths = Directory.EnumerateFiles(currentPath);
                }
                catch (UnauthorizedAccessException)
                {
                    // Can't enumerate files in this directory, continue to directories
                }
                catch (DirectoryNotFoundException)
                {
                    // Directory was deleted, skip it
                    continue;
                }

                // Process files (yield outside try-catch)
                foreach (var filePath in filePaths)
                {
                    FileInfo? file = null;
                    try
                    {
                        file = new FileInfo(filePath);
                        filesChecked++;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }
                    catch (PathTooLongException)
                    {
                        continue;
                    }
                    catch
                    {
                        continue;
                    }

                    if (file != null && file.Name.ToLowerInvariant().Contains(searchLower))
                    {
                        var item = FileSystemItem.FromFileInfo(file);
                        item.ParentPath = file.Directory?.FullName;
                        
                        // If file is not in root directory, show full path as display name
                        if (!string.IsNullOrEmpty(rootPath) && 
                            !string.IsNullOrEmpty(item.ParentPath) &&
                            !item.ParentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
                        {
                            item.DisplayName = item.Path; // Show full path
                        }
                        else
                        {
                            item.DisplayName = item.Name;
                        }
                        
                        yield return item;
                    }
                }

                // Enumerate subdirectories (more fault-tolerant than GetDirectories)
                System.Diagnostics.Debug.WriteLine($"[Search] Enumerating directories in: {currentPath}");
                var directoriesToProcess = new List<string>();
                try
                {
                    // Collect directory paths first (this might throw if enumeration fails)
                    foreach (var dirPath in Directory.EnumerateDirectories(currentPath))
                    {
                        directoriesToProcess.Add(dirPath);
                        System.Diagnostics.Debug.WriteLine($"[Search] Found directory: {dirPath}");
                    }
                    System.Diagnostics.Debug.WriteLine($"[Search] Found {directoriesToProcess.Count} directories in {currentPath}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Search] UnauthorizedAccessException enumerating {currentPath}: {ex.Message}");
                }
                catch (DirectoryNotFoundException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Search] DirectoryNotFoundException enumerating {currentPath}: {ex.Message}");
                    continue;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Search] Exception enumerating {currentPath}: {ex.GetType().Name} - {ex.Message}");
                }

                // Process directories (yield outside try-catch)
                int dirsProcessed = 0;
                int dirsMatched = 0;
                foreach (var dirPath in directoriesToProcess)
                {
                    DirectoryInfo? subdir = null;
                    try
                    {
                        subdir = new DirectoryInfo(dirPath);
                        dirsProcessed++;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Search] Cannot create DirectoryInfo for {dirPath} - UnauthorizedAccess");
                        continue;
                    }
                    catch (PathTooLongException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Search] Cannot create DirectoryInfo for {dirPath} - PathTooLong");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Search] Cannot create DirectoryInfo for {dirPath} - {ex.GetType().Name}: {ex.Message}");
                        continue;
                    }

                    if (subdir != null)
                    {
                        var dirName = subdir.Name;
                        var dirNameLower = dirName.ToLowerInvariant();
                        var matches = dirNameLower.Contains(searchLower);
                        System.Diagnostics.Debug.WriteLine($"[Search] Checking directory '{dirName}' (lower: '{dirNameLower}') against '{searchLower}': {matches}");
                        
                        // Check if directory name matches
                        if (matches)
                        {
                            dirsMatched++;
                            System.Diagnostics.Debug.WriteLine($"[Search] MATCH! Directory '{dirName}' matches search '{searchTerm}'");
                            var item = FileSystemItem.FromDirectoryInfo(subdir);
                            item.ParentPath = subdir.Parent?.FullName;
                            
                            // If directory is not in root directory, show full path as display name
                            if (!string.IsNullOrEmpty(rootPath) && 
                                !string.IsNullOrEmpty(item.ParentPath) &&
                                !item.ParentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
                            {
                                item.DisplayName = item.Path; // Show full path
                            }
                            else
                            {
                                item.DisplayName = item.Name;
                            }
                            
                            yield return item;
                        }
                        
                        // Add to stack for recursive search (but skip Windows directory)
                        try
                        {
                            var subdirPathLower = subdir.FullName.ToLowerInvariant();
                            if (!subdirPathLower.Contains("\\windows\\") && !subdirPathLower.EndsWith("\\windows"))
                            {
                                searchStack.Push(subdir.FullName);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[Search] Skipping Windows directory from recursive search: {subdir.FullName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Search] Cannot push {subdir.FullName} to stack: {ex.Message}");
                        }
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[Search] Processed {dirsProcessed} directories, {dirsMatched} matched in {currentPath}");
                
                // Update progress every 10 folders or 100 files to avoid too many UI updates
                int totalChecked = foldersChecked + filesChecked;
                if (progressCallback != null && (totalChecked - lastProgressUpdate >= 10 || foldersChecked % 10 == 0))
                {
                    progressCallback(foldersChecked, filesChecked);
                    lastProgressUpdate = totalChecked;
                }
            }
            
            // Final progress update
            if (progressCallback != null)
            {
                progressCallback(foldersChecked, filesChecked);
            }
            
            System.Diagnostics.Debug.WriteLine($"[Search] Search completed. Checked {foldersChecked} folders, {filesChecked} files");
        }
    }
}

