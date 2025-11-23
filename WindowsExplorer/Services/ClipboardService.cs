using System.Collections.Generic;
using System.IO;
using System.Linq;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace WindowsExplorer.Services
{
    /// <summary>
    /// Service for handling clipboard operations with files.
    /// </summary>
    public class ClipboardService
    {
        private static readonly string InternalFormat = "WindowsExplorer.Internal";
        private static bool _isCutOperation = false;

        /// <summary>
        /// Copies files to the clipboard.
        /// </summary>
        public static async System.Threading.Tasks.Task CopyFilesAsync(IEnumerable<string> filePaths, bool isCut = false)
        {
            _isCutOperation = isCut;
            
            var dataPackage = new DataPackage();
            var fileList = new List<IStorageItem>();
            
            foreach (var path in filePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var file = await StorageFile.GetFileFromPathAsync(path);
                        fileList.Add(file);
                    }
                    else if (Directory.Exists(path))
                    {
                        var folder = await StorageFolder.GetFolderFromPathAsync(path);
                        fileList.Add(folder);
                    }
                }
                catch
                {
                    // Skip files/folders that can't be accessed
                    continue;
                }
            }
            
            if (fileList.Count > 0)
            {
                dataPackage.SetStorageItems(fileList);
                
                // Add internal format for tracking cut vs copy
                dataPackage.SetText(isCut ? "CUT" : "COPY");
                dataPackage.Properties.Description = isCut ? "Cut" : "Copy";
                
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush(); // Make clipboard content available after app closes
            }
        }

        /// <summary>
        /// Checks if the clipboard contains files.
        /// </summary>
        public static bool HasFiles()
        {
            var dataPackageView = Clipboard.GetContent();
            return dataPackageView.Contains(StandardDataFormats.StorageItems);
        }

        /// <summary>
        /// Gets files from the clipboard.
        /// </summary>
        public static async System.Threading.Tasks.Task<IReadOnlyList<IStorageItem>> GetFilesAsync()
        {
            var dataPackageView = Clipboard.GetContent();
            
            if (dataPackageView.Contains(StandardDataFormats.StorageItems))
            {
                return await dataPackageView.GetStorageItemsAsync();
            }
            
            return Array.Empty<IStorageItem>();
        }

        /// <summary>
        /// Checks if the last operation was a cut (move) operation.
        /// </summary>
        public static async System.Threading.Tasks.Task<bool> IsCutOperationAsync()
        {
            var dataPackageView = Clipboard.GetContent();
            
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                try
                {
                    var text = await dataPackageView.GetTextAsync();
                    return text == "CUT";
                }
                catch
                {
                    // If we can't read the text, assume it's from external source (copy)
                    return false;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Pastes files from clipboard to the target directory.
        /// </summary>
        public static async System.Threading.Tasks.Task<bool> PasteFilesAsync(string targetDirectory)
        {
            if (!Directory.Exists(targetDirectory))
            {
                return false;
            }
            
            var files = await GetFilesAsync();
            if (files.Count == 0)
            {
                return false;
            }
            
            bool isCut = await IsCutOperationAsync();
            bool success = true;
            
            foreach (var item in files)
            {
                try
                {
                    string sourcePath = item.Path;
                    string targetPath = Path.Combine(targetDirectory, item.Name);
                    
                    if (item is StorageFile file)
                    {
                        if (isCut)
                        {
                            // Move file
                            if (File.Exists(targetPath))
                            {
                                // Handle name conflicts
                                targetPath = GetUniquePath(targetPath);
                            }
                            File.Move(sourcePath, targetPath);
                        }
                        else
                        {
                            // Copy file
                            if (File.Exists(targetPath))
                            {
                                targetPath = GetUniquePath(targetPath);
                            }
                            File.Copy(sourcePath, targetPath);
                        }
                    }
                    else if (item is StorageFolder folder)
                    {
                        if (isCut)
                        {
                            // Move folder
                            if (Directory.Exists(targetPath))
                            {
                                targetPath = GetUniquePath(targetPath);
                            }
                            Directory.Move(sourcePath, targetPath);
                        }
                        else
                        {
                            // Copy folder recursively
                            if (Directory.Exists(targetPath))
                            {
                                targetPath = GetUniquePath(targetPath);
                            }
                            CopyDirectory(sourcePath, targetPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error or show message
                    System.Diagnostics.Debug.WriteLine($"Error pasting {item.Name}: {ex.Message}");
                    success = false;
                }
            }
            
            // Clear cut flag after paste
            if (isCut)
            {
                await CopyFilesAsync(files.Select(f => f.Path), false);
            }
            
            return success;
        }

        private static string GetUniquePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return path;
            }
            
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            string fileName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            int counter = 1;
            
            string newPath;
            do
            {
                string newFileName = $"{fileName} ({counter}){extension}";
                newPath = Path.Combine(directory, newFileName);
                counter++;
            } while (File.Exists(newPath) || Directory.Exists(newPath));
            
            return newPath;
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            
            // Copy files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, true);
            }
            
            // Copy subdirectories
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                string destSubDir = Path.Combine(targetDir, dirName);
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}

