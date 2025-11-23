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

    }
}

