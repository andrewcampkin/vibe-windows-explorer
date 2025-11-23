using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsExplorer.Models;

namespace WindowsExplorer.Services
{
    /// <summary>
    /// Service for handling file system search operations.
    /// </summary>
    public class SearchService
    {
        private readonly FileSystemService _fileSystemService;

        public SearchService(FileSystemService fileSystemService)
        {
            _fileSystemService = fileSystemService;
        }

        /// <summary>
        /// Searches for files and folders matching the search term, starting from the root path.
        /// Uses breadth-first search to show top-level results first.
        /// </summary>
        public async Task<SearchResult> SearchAsync(
            string rootPath,
            string searchTerm,
            Action<int, int>? progressCallback,
            Action<FileSystemItem>? itemFoundCallback,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new SearchResult { Items = new List<FileSystemItem>() };
            }

            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return new SearchResult { Items = new List<FileSystemItem>() };
            }

            var result = new SearchResult();
            var searchLower = searchTerm.ToLowerInvariant();
            
            // Use Queue for breadth-first search (top-level items appear first)
            var searchQueue = new Queue<string>();
            searchQueue.Enqueue(rootPath);

            int foldersChecked = 0;
            int filesChecked = 0;
            var items = new List<FileSystemItem>();

            // Track which directories we've already processed to avoid duplicates
            var processedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (searchQueue.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                var currentPath = searchQueue.Dequeue();

                // Skip if already processed
                if (processedDirectories.Contains(currentPath))
                {
                    continue;
                }
                processedDirectories.Add(currentPath);

                // Skip Windows directory to avoid getting stuck
                var pathLower = currentPath.ToLowerInvariant();
                if (pathLower.Contains("\\windows\\") || pathLower.EndsWith("\\windows"))
                {
                    continue;
                }

                if (!Directory.Exists(currentPath))
                {
                    continue;
                }

                // Check cancellation before processing
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    foldersChecked++;

                    // Process files in current directory
                    int filesInDir = await ProcessFilesInDirectoryAsync(
                        currentPath,
                        searchLower,
                        items,
                        itemFoundCallback,
                        cancellationToken);
                    filesChecked += filesInDir;

                    // Process directories in current directory
                    var subdirectories = await ProcessDirectoriesInDirectoryAsync(
                        currentPath,
                        rootPath,
                        searchLower,
                        items,
                        itemFoundCallback,
                        searchQueue,
                        cancellationToken);

                    // Update progress periodically
                    if (progressCallback != null && (foldersChecked % 10 == 0 || filesChecked % 100 == 0))
                    {
                        progressCallback(foldersChecked, filesChecked);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip directories we can't access
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    // Directory was deleted, skip it
                    continue;
                }
                catch (Exception)
                {
                    // Other errors - skip this directory
                    continue;
                }
            }

            // Final progress update
            if (progressCallback != null && !cancellationToken.IsCancellationRequested)
            {
                progressCallback(foldersChecked, filesChecked);
            }

            result.Items = items;
            result.FoldersChecked = foldersChecked;
            result.FilesChecked = filesChecked;

            return result;
        }

        private async Task<int> ProcessFilesInDirectoryAsync(
            string directoryPath,
            string searchLower,
            List<FileSystemItem> items,
            Action<FileSystemItem>? itemFoundCallback,
            CancellationToken cancellationToken)
        {
            int filesChecked = 0;
            
            await Task.Run(() =>
            {
                try
                {
                    foreach (var filePath in Directory.EnumerateFiles(directoryPath))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        try
                        {
                            var file = new FileInfo(filePath);
                            filesChecked++;

                            if (file.Name.ToLowerInvariant().Contains(searchLower))
                            {
                                var item = FileSystemItem.FromFileInfo(file);
                                item.ParentPath = file.Directory?.FullName;
                                
                                // Set display name based on whether it's in root or subdirectory
                                if (!string.IsNullOrEmpty(item.ParentPath) &&
                                    !item.ParentPath.Equals(Path.GetDirectoryName(directoryPath), StringComparison.OrdinalIgnoreCase))
                                {
                                    item.DisplayName = item.Path;
                                }
                                else
                                {
                                    item.DisplayName = item.Name;
                                }

                                lock (items)
                                {
                                    items.Add(item);
                                }

                                // Notify callback immediately for progressive display
                                itemFoundCallback?.Invoke(item);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Skip files we can't access
                            continue;
                        }
                        catch (PathTooLongException)
                        {
                            // Skip files with paths that are too long
                            continue;
                        }
                        catch
                        {
                            // Skip other errors
                            continue;
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Can't enumerate files in this directory
                }
                catch (DirectoryNotFoundException)
                {
                    // Directory was deleted
                }
            }, cancellationToken);
            
            return filesChecked;
        }

        private async Task<List<string>> ProcessDirectoriesInDirectoryAsync(
            string directoryPath,
            string rootPath,
            string searchLower,
            List<FileSystemItem> items,
            Action<FileSystemItem>? itemFoundCallback,
            Queue<string> searchQueue,
            CancellationToken cancellationToken)
        {
            var subdirectories = new List<string>();

            await Task.Run(() =>
            {
                try
                {
                    foreach (var dirPath in Directory.EnumerateDirectories(directoryPath))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        try
                        {
                            var subdir = new DirectoryInfo(dirPath);
                            var dirNameLower = subdir.Name.ToLowerInvariant();

                            // Check if directory name matches
                            if (dirNameLower.Contains(searchLower))
                            {
                                var item = FileSystemItem.FromDirectoryInfo(subdir);
                                item.ParentPath = subdir.Parent?.FullName;

                                // Set display name based on whether it's in root or subdirectory
                                if (!string.IsNullOrEmpty(rootPath) &&
                                    !string.IsNullOrEmpty(item.ParentPath) &&
                                    !item.ParentPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    item.DisplayName = item.Path;
                                }
                                else
                                {
                                    item.DisplayName = item.Name;
                                }

                                lock (items)
                                {
                                    items.Add(item);
                                }

                                // Notify callback immediately for progressive display
                                itemFoundCallback?.Invoke(item);
                            }

                            // Add to queue for recursive search (but skip Windows directory)
                            var subdirPathLower = subdir.FullName.ToLowerInvariant();
                            if (!subdirPathLower.Contains("\\windows\\") && !subdirPathLower.EndsWith("\\windows"))
                            {
                                subdirectories.Add(subdir.FullName);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Skip directories we can't access
                            continue;
                        }
                        catch (PathTooLongException)
                        {
                            // Skip directories with paths that are too long
                            continue;
                        }
                        catch
                        {
                            // Skip other errors
                            continue;
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Can't enumerate directories
                }
                catch (DirectoryNotFoundException)
                {
                    // Directory was deleted
                }
            }, cancellationToken);

            // Add subdirectories to queue after processing (to maintain breadth-first order)
            foreach (var subdir in subdirectories)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    searchQueue.Enqueue(subdir);
                }
            }

            return subdirectories;
        }
    }

    /// <summary>
    /// Result of a search operation.
    /// </summary>
    public class SearchResult
    {
        public List<FileSystemItem> Items { get; set; } = new();
        public int FoldersChecked { get; set; }
        public int FilesChecked { get; set; }
    }
}

