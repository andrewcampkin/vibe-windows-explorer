using System.IO;

namespace WindowsExplorer.Models
{
    /// <summary>
    /// Represents a file or folder in the file system.
    /// </summary>
    public class FileSystemItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Type { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty; // For search results, may include path
        public string? ParentPath { get; set; } // Parent directory path for navigation

        public static FileSystemItem FromFileInfo(FileInfo fileInfo)
        {
            return new FileSystemItem
            {
                Name = fileInfo.Name,
                Path = fileInfo.FullName,
                DisplayName = fileInfo.Name,
                IsDirectory = false,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                Type = fileInfo.Extension.TrimStart('.').ToUpper() + " File"
            };
        }

        public static FileSystemItem FromDirectoryInfo(DirectoryInfo dirInfo)
        {
            return new FileSystemItem
            {
                Name = dirInfo.Name,
                Path = dirInfo.FullName,
                DisplayName = dirInfo.Name,
                IsDirectory = true,
                Size = 0,
                LastModified = dirInfo.LastWriteTime,
                Type = "File folder"
            };
        }
    }
}

