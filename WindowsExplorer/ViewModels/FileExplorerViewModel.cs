using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WindowsExplorer.Models;
using WindowsExplorer.Services;

namespace WindowsExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for the file explorer window.
    /// </summary>
    public enum SortColumn
    {
        Name,
        Size,
        Type,
        DateModified
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public class FileExplorerViewModel : INotifyPropertyChanged
    {
        private readonly FileSystemService _fileSystemService;
        private string _currentPath = string.Empty;
        private string _searchText = string.Empty;
        private SortColumn _sortColumn = SortColumn.Name;
        private SortDirection _sortDirection = SortDirection.Ascending;
        private readonly Stack<string> _backHistory = new();
        private readonly Stack<string> _forwardHistory = new();
        private readonly List<FileSystemItem> _allItems = new();
        private CancellationTokenSource? _searchCancellationTokenSource;
        private bool _isSearchingRecursive = false;
        private bool _isSearching = false;
        private int _foldersChecked = 0;
        private int _filesChecked = 0;
        private Timer? _searchDebounceTimer;
        private const int SearchDebounceMilliseconds = 300;

        public FileExplorerViewModel()
        {
            _fileSystemService = new FileSystemService();
            Items = new ObservableCollection<FileSystemItem>();
            NavigateToPath(_fileSystemService.GetDefaultPath());
        }

        public ObservableCollection<FileSystemItem> Items { get; }

        public bool IsSearching
        {
            get => _isSearching;
            private set
            {
                if (_isSearching != value)
                {
                    _isSearching = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SearchStatusText));
                }
            }
        }

        public int FoldersChecked
        {
            get => _foldersChecked;
            private set
            {
                if (_foldersChecked != value)
                {
                    _foldersChecked = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SearchStatusText));
                }
            }
        }

        public int FilesChecked
        {
            get => _filesChecked;
            private set
            {
                if (_filesChecked != value)
                {
                    _filesChecked = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SearchStatusText));
                }
            }
        }

        public string SearchStatusText
        {
            get
            {
                if (!IsSearching)
                {
                    return string.Empty;
                }
                return $"Searching... ({FoldersChecked} folders, {FilesChecked} files checked)";
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetSearchText(value, triggerSearch: true);
            }
        }

        private void SetSearchText(string value, bool triggerSearch)
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                
                if (!triggerSearch)
                {
                    // Just update the value without triggering search (e.g., during navigation)
                    return;
                }
                
                // Cancel any pending search operations
                CancelAndDisposeSearch();
                
                // If search text is empty, clear immediately
                if (string.IsNullOrWhiteSpace(_searchText))
                {
                    ApplySearchFilter();
                }
                else
                {
                    // Debounce the search - wait for user to stop typing
                    _searchDebounceTimer = new Timer(async _ =>
                    {
                        _searchDebounceTimer?.Dispose();
                        _searchDebounceTimer = null;
                        await ApplySearchFilterAsync();
                    }, null, SearchDebounceMilliseconds, Timeout.Infinite);
                }
            }
        }

        public string CurrentPath
        {
            get => _currentPath;
            private set
            {
                if (_currentPath != value)
                {
                    _currentPath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(CanGoForward));
                    OnPropertyChanged(nameof(CanGoUp));
                }
            }
        }

        public bool CanGoBack => _backHistory.Count > 0;
        public bool CanGoForward => _forwardHistory.Count > 0;
        public bool CanGoUp
        {
            get
            {
                if (string.IsNullOrEmpty(CurrentPath))
                {
                    return false; // Can't go up from PC root
                }
                var parentPath = _fileSystemService.GetParentPath(CurrentPath);
                return parentPath != null; // Can go up if parent exists (including empty string for drive roots)
            }
        }

        public SortColumn CurrentSortColumn
        {
            get => _sortColumn;
            private set
            {
                if (_sortColumn != value)
                {
                    _sortColumn = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SortColumnName));
                    OnPropertyChanged(nameof(SortColumnSize));
                    OnPropertyChanged(nameof(SortColumnType));
                    OnPropertyChanged(nameof(SortColumnDateModified));
                }
            }
        }

        public SortDirection CurrentSortDirection
        {
            get => _sortDirection;
            private set
            {
                if (_sortDirection != value)
                {
                    _sortDirection = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SortColumnName));
                    OnPropertyChanged(nameof(SortColumnSize));
                    OnPropertyChanged(nameof(SortColumnType));
                    OnPropertyChanged(nameof(SortColumnDateModified));
                }
            }
        }

        // Properties for UI to bind to show sort indicators
        public string SortColumnName => CurrentSortColumn == SortColumn.Name 
            ? (_sortDirection == SortDirection.Ascending ? "Name ▲" : "Name ▼") 
            : "Name";
        public string SortColumnSize => CurrentSortColumn == SortColumn.Size 
            ? (_sortDirection == SortDirection.Ascending ? "Size ▲" : "Size ▼") 
            : "Size";
        public string SortColumnType => CurrentSortColumn == SortColumn.Type 
            ? (_sortDirection == SortDirection.Ascending ? "Type ▲" : "Type ▼") 
            : "Type";
        public string SortColumnDateModified => CurrentSortColumn == SortColumn.DateModified 
            ? (_sortDirection == SortDirection.Ascending ? "Date Modified ▲" : "Date Modified ▼") 
            : "Date Modified";

        public void NavigateToPath(string path, bool addToHistory = true)
        {
            // Handle empty string (PC root) - always valid
            if (string.IsNullOrEmpty(path))
            {
                // Add current path to back history if we're navigating to a new path
                if (addToHistory && !string.IsNullOrEmpty(CurrentPath) && CurrentPath != path)
                {
                    _backHistory.Push(CurrentPath);
                    _forwardHistory.Clear(); // Clear forward history when navigating to new path
                }

                CurrentPath = string.Empty;
                // Cancel any pending search and debounce timer
                CancelAndDisposeSearch();
                // Clear search when navigating to a new path (without triggering search)
                SetSearchText(string.Empty, triggerSearch: false);
                RefreshItems();
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoForward));
                OnPropertyChanged(nameof(CanGoUp));
                OnPropertyChanged(nameof(CanPaste));
                return;
            }

            // Check if path exists
            if (!_fileSystemService.PathExists(path))
            {
                // If path is a file, navigate to its parent
                if (File.Exists(path))
                {
                    var parent = _fileSystemService.GetParentPath(path);
                    if (parent != null)
                    {
                        NavigateToPath(parent, addToHistory);
                    }
                    return;
                }

                // Invalid path - don't navigate
                return;
            }

            // If path is a directory, navigate to it
            if (Directory.Exists(path))
            {
                // Add current path to back history if we're navigating to a new path
                if (addToHistory && !string.IsNullOrEmpty(CurrentPath) && CurrentPath != path)
                {
                    _backHistory.Push(CurrentPath);
                    _forwardHistory.Clear(); // Clear forward history when navigating to new path
                }

                CurrentPath = path;
                // Cancel any pending search and debounce timer
                CancelAndDisposeSearch();
                // Clear search when navigating to a new path (without triggering search)
                SetSearchText(string.Empty, triggerSearch: false);
                RefreshItems();
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoForward));
                OnPropertyChanged(nameof(CanGoUp));
                OnPropertyChanged(nameof(CanPaste));
            }
        }

        public void NavigateBack()
        {
            if (_backHistory.Count > 0)
            {
                var previousPath = _backHistory.Pop();
                if (!string.IsNullOrEmpty(CurrentPath))
                {
                    _forwardHistory.Push(CurrentPath);
                }
                NavigateToPath(previousPath, false);
            }
        }

        public void NavigateForward()
        {
            if (_forwardHistory.Count > 0)
            {
                var nextPath = _forwardHistory.Pop();
                if (!string.IsNullOrEmpty(CurrentPath))
                {
                    _backHistory.Push(CurrentPath);
                }
                NavigateToPath(nextPath, false);
            }
        }

        public void NavigateUp()
        {
            if (string.IsNullOrEmpty(CurrentPath))
            {
                return; // Can't go up from PC root
            }
            
            var parentPath = _fileSystemService.GetParentPath(CurrentPath);
            // parentPath can be null (no parent) or empty string (PC root from drive root)
            if (parentPath != null)
            {
                NavigateToPath(parentPath);
            }
        }

        public void NavigateToItem(FileSystemItem item)
        {
            if (item.IsDirectory)
            {
                NavigateToPath(item.Path);
            }
        }

        public async void RefreshItems()
        {
            _allItems.Clear();
            var items = _fileSystemService.GetItems(CurrentPath);
            _allItems.AddRange(items);
            
            // If there's no search text, directly update Items without going through search filter
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                var dispatcherQueue = App.MainWindow?.DispatcherQueue;
                if (dispatcherQueue != null)
                {
                    var updateTcs = new TaskCompletionSource<bool>();
                    if (dispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            Items.Clear();
                            var sortedItems = _sortDirection == SortDirection.Ascending
                                ? SortItemsAscending(_allItems, _sortColumn)
                                : SortItemsDescending(_allItems, _sortColumn);
                            
                            foreach (var item in sortedItems)
                            {
                                Items.Add(item);
                            }
                            updateTcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            updateTcs.SetException(ex);
                        }
                    }))
                    {
                        await updateTcs.Task;
                    }
                }
                else
                {
                    // Fallback if DispatcherQueue not available
                    Items.Clear();
                    var sortedItems = _sortDirection == SortDirection.Ascending
                        ? SortItemsAscending(_allItems, _sortColumn)
                        : SortItemsDescending(_allItems, _sortColumn);
                    
                    foreach (var item in sortedItems)
                    {
                        Items.Add(item);
                    }
                }
            }
            else
            {
                // There's a search active, use the search filter
                await ApplySearchFilterAsync();
            }
            
            OnPropertyChanged(nameof(CanPaste));
        }

        public bool CanPaste
        {
            get
            {
                return ClipboardService.HasFiles();
            }
        }

        public async Task CopySelectedItemsAsync(IEnumerable<FileSystemItem> items)
        {
            var paths = items.Select(item => item.Path).ToList();
            await ClipboardService.CopyFilesAsync(paths, isCut: false);
            OnPropertyChanged(nameof(CanPaste));
        }

        public async Task CutSelectedItemsAsync(IEnumerable<FileSystemItem> items)
        {
            var paths = items.Select(item => item.Path).ToList();
            await ClipboardService.CopyFilesAsync(paths, isCut: true);
            OnPropertyChanged(nameof(CanPaste));
        }

        public async Task PasteAsync()
        {
            if (string.IsNullOrEmpty(CurrentPath))
            {
                return; // Can't paste to PC root
            }
            
            if (!Directory.Exists(CurrentPath))
            {
                return;
            }
            
            bool success = await ClipboardService.PasteFilesAsync(CurrentPath);
            if (success)
            {
                RefreshItems();
            }
        }

        public void SortBy(SortColumn column)
        {
            // If clicking the same column, toggle direction; otherwise, set to ascending
            if (CurrentSortColumn == column)
            {
                CurrentSortDirection = CurrentSortDirection == SortDirection.Ascending 
                    ? SortDirection.Descending 
                    : SortDirection.Ascending;
            }
            else
            {
                CurrentSortColumn = column;
                CurrentSortDirection = SortDirection.Ascending;
            }
            
            ApplySearchFilter();
        }

        private void CancelAndDisposeSearch()
        {
            // Cancel any pending debounce timer
            _searchDebounceTimer?.Dispose();
            _searchDebounceTimer = null;
            
            // Cancel any ongoing search
            var cts = _searchCancellationTokenSource;
            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
                try
                {
                    cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
                _searchCancellationTokenSource = null;
            }
        }

        private async Task ApplySearchFilterAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[Search] ApplySearchFilterAsync called. CurrentPath: '{CurrentPath}', SearchText: '{_searchText}'");
            
            // Cancel any ongoing search
            CancelAndDisposeSearch();
            _searchCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _searchCancellationTokenSource?.Token ?? CancellationToken.None;

            // Capture the current search text to ensure we use the latest value
            var currentSearchText = _searchText;
            System.Diagnostics.Debug.WriteLine($"[Search] Using search text: '{currentSearchText}'");

            // Get DispatcherQueue - must be on UI thread for ObservableCollection operations
            var dispatcherQueue = App.MainWindow?.DispatcherQueue;
            if (dispatcherQueue == null)
            {
                // Can't proceed without DispatcherQueue
                return;
            }

            // Clear items on UI thread
            var clearTcs = new TaskCompletionSource<bool>();
            if (!dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    Items.Clear();
                    clearTcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    clearTcs.SetException(ex);
                }
            }))
            {
                // Failed to enqueue, can't proceed
                return;
            }
            await clearTcs.Task;

            if (string.IsNullOrWhiteSpace(currentSearchText))
            {
                // No search - show current directory items
                _isSearchingRecursive = false;
                IsSearching = false;
                
                if (cancellationToken.IsCancellationRequested) return;
                
                var filteredItems = new List<FileSystemItem>();
                filteredItems.AddRange(_allItems);

                // Sort the filtered items
                var sortedItems = _sortDirection == SortDirection.Ascending
                    ? SortItemsAscending(filteredItems, _sortColumn)
                    : SortItemsDescending(filteredItems, _sortColumn);

                // Add sorted items to collection on UI thread
                var addTcs = new TaskCompletionSource<bool>();
                if (!dispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        foreach (var item in sortedItems)
                        {
                            if (cancellationToken.IsCancellationRequested) return;
                            Items.Add(item);
                        }
                        addTcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        addTcs.SetException(ex);
                    }
                }))
                {
                    // Failed to enqueue
                    return;
                }
                await addTcs.Task;
            }
            else
            {
                // Check if we should do recursive search
                // Only do recursive if we're in a directory (not at PC root)
                if (!string.IsNullOrEmpty(CurrentPath) && Directory.Exists(CurrentPath))
                {
                    _isSearchingRecursive = true;
                    await SearchRecursiveAsync(CurrentPath, currentSearchText, cancellationToken);
                }
                else
                {
                    // At PC root or invalid path - just filter current items
                    _isSearchingRecursive = false;
                    IsSearching = false; // Not searching recursively
                    
                    if (cancellationToken.IsCancellationRequested) return;
                    
                    var searchLower = currentSearchText.ToLowerInvariant();
                    var filteredItems = _allItems.Where(item => 
                        item.Name.ToLowerInvariant().Contains(searchLower)).ToList();

                    var sortedItems = _sortDirection == SortDirection.Ascending
                        ? SortItemsAscending(filteredItems, _sortColumn)
                        : SortItemsDescending(filteredItems, _sortColumn);

                    // Add items on UI thread
                    var addTcs2 = new TaskCompletionSource<bool>();
                    if (!dispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            foreach (var item in sortedItems)
                            {
                                if (cancellationToken.IsCancellationRequested) return;
                                Items.Add(item);
                            }
                            addTcs2.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            addTcs2.SetException(ex);
                        }
                    }))
                    {
                        // Failed to enqueue
                        return;
                    }
                    await addTcs2.Task;
                }
            }
        }

        private void ApplySearchFilter()
        {
            // Synchronous version for immediate updates (e.g., when clearing search)
            _ = ApplySearchFilterAsync();
        }

        private async Task SearchRecursiveAsync(string rootPath, string searchTerm, CancellationToken cancellationToken)
        {
            // Get DispatcherQueue for UI thread operations
            var dispatcherQueue = App.MainWindow?.DispatcherQueue;
            if (dispatcherQueue == null)
            {
                return;
            }

            // Clear items on UI thread
            var clearTcs = new TaskCompletionSource<bool>();
            if (!dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    Items.Clear();
                    clearTcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    clearTcs.SetException(ex);
                }
            }))
            {
                return;
            }
            await clearTcs.Task;
            
            // Reset search status on UI thread
            var statusTcs = new TaskCompletionSource<bool>();
            if (!dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    IsSearching = true;
                    FoldersChecked = 0;
                    FilesChecked = 0;
                    statusTcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    statusTcs.SetException(ex);
                }
            }))
            {
                return;
            }
            await statusTcs.Task;
            
            var foundItems = new List<FileSystemItem>();

            try
            {
                // Use lazy enumeration - only enumerate items as needed (memory efficient)
                // Collect results on background thread
                System.Diagnostics.Debug.WriteLine($"[Search] SearchRecursiveAsync starting for path: '{rootPath}', term: '{searchTerm}'");
                await Task.Run(() =>
                {
                    int itemsFound = 0;
                    foreach (var item in _fileSystemService.SearchRecursive(rootPath, searchTerm, 
                        (folders, files) =>
                        {
                            // Update progress on UI thread
                            if (App.MainWindow != null)
                            {
                                _ = App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                                {
                                    FoldersChecked = folders;
                                    FilesChecked = files;
                                });
                            }
                        }))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Search] Search cancelled after finding {itemsFound} items");
                            return;
                        }

                        foundItems.Add(item);
                        itemsFound++;
                        if (itemsFound <= 10) // Log first 10 items
                        {
                            System.Diagnostics.Debug.WriteLine($"[Search] Found item #{itemsFound}: {item.Name} at {item.Path}");
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[Search] Search enumeration completed. Found {itemsFound} items total");
                }, cancellationToken);

                // Update UI with all found items (still memory efficient - we only hold FileSystemItem references, not file contents)
                if (!cancellationToken.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"[Search] Sorting {foundItems.Count} found items");
                    // Sort the results
                    var sorted = _sortDirection == SortDirection.Ascending
                        ? SortItemsAscending(foundItems, _sortColumn)
                        : SortItemsDescending(foundItems, _sortColumn);
                    
                    System.Diagnostics.Debug.WriteLine($"[Search] Adding {sorted.Count()} items to UI");
                    // Add to UI collection on UI thread
                    var addTcs = new TaskCompletionSource<bool>();
                    if (!dispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            int added = 0;
                            foreach (var item in sorted)
                            {
                                if (cancellationToken.IsCancellationRequested) break;
                                Items.Add(item);
                                added++;
                            }
                            System.Diagnostics.Debug.WriteLine($"[Search] Added {added} items to UI collection");
                            addTcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Search] Error adding items to UI: {ex.GetType().Name} - {ex.Message}");
                            addTcs.SetException(ex);
                        }
                    }))
                    {
                        System.Diagnostics.Debug.WriteLine($"[Search] Failed to enqueue UI update");
                        return;
                    }
                    await addTcs.Task;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Search] Search was cancelled, not updating UI");
                }
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled, ignore
            }
            catch (Exception ex)
            {
                // Log or handle other exceptions - don't silently fail
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Set IsSearching = false on UI thread (fire and forget in finally)
                if (dispatcherQueue != null)
                {
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            IsSearching = false;
                        }
                        catch
                        {
                            // Ignore exceptions in finally
                        }
                    });
                }
            }
        }

        private IEnumerable<FileSystemItem> SortItemsAscending(List<FileSystemItem> items, SortColumn column)
        {
            // Always show directories first, then files
            var directories = items.Where(i => i.IsDirectory);
            var files = items.Where(i => !i.IsDirectory);

            var sortedDirectories = column switch
            {
                SortColumn.Name => directories.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                SortColumn.Size => directories.OrderBy(i => i.Size),
                SortColumn.Type => directories.OrderBy(i => i.Type, StringComparer.OrdinalIgnoreCase),
                SortColumn.DateModified => directories.OrderBy(i => i.LastModified),
                _ => directories
            };

            var sortedFiles = column switch
            {
                SortColumn.Name => files.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                SortColumn.Size => files.OrderBy(i => i.Size),
                SortColumn.Type => files.OrderBy(i => i.Type, StringComparer.OrdinalIgnoreCase),
                SortColumn.DateModified => files.OrderBy(i => i.LastModified),
                _ => files
            };

            return sortedDirectories.Concat(sortedFiles);
        }

        private IEnumerable<FileSystemItem> SortItemsDescending(List<FileSystemItem> items, SortColumn column)
        {
            // Always show directories first, then files
            var directories = items.Where(i => i.IsDirectory);
            var files = items.Where(i => !i.IsDirectory);

            var sortedDirectories = column switch
            {
                SortColumn.Name => directories.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase),
                SortColumn.Size => directories.OrderByDescending(i => i.Size),
                SortColumn.Type => directories.OrderByDescending(i => i.Type, StringComparer.OrdinalIgnoreCase),
                SortColumn.DateModified => directories.OrderByDescending(i => i.LastModified),
                _ => directories
            };

            var sortedFiles = column switch
            {
                SortColumn.Name => files.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase),
                SortColumn.Size => files.OrderByDescending(i => i.Size),
                SortColumn.Type => files.OrderByDescending(i => i.Type, StringComparer.OrdinalIgnoreCase),
                SortColumn.DateModified => files.OrderByDescending(i => i.LastModified),
                _ => files
            };

            return sortedDirectories.Concat(sortedFiles);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

