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
        private readonly SearchService _searchService;
        private string _currentPath = string.Empty;
        private string _searchText = string.Empty;
        private SortColumn _sortColumn = SortColumn.Name;
        private SortDirection _sortDirection = SortDirection.Ascending;
        private readonly Stack<string> _backHistory = new();
        private readonly Stack<string> _forwardHistory = new();
        private readonly List<FileSystemItem> _allItems = new();
        private CancellationTokenSource? _searchCancellationTokenSource;
        private string? _activeSearchPath; // Track which path the active search is for
        private bool _isSearching = false;
        private bool _isSearchPaused = false;
        private int _foldersChecked = 0;
        private int _filesChecked = 0;
        private Timer? _searchDebounceTimer;
        private const int SearchDebounceMilliseconds = 300;
        private const int MaxSearchResults = 20;
        private Queue<string>? _pausedSearchQueue;
        private HashSet<string>? _pausedProcessedDirs;

        public FileExplorerViewModel()
        {
            _fileSystemService = new FileSystemService();
            _searchService = new SearchService(_fileSystemService);
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
                    OnPropertyChanged(nameof(IsSearchPaused));
                    OnPropertyChanged(nameof(StopSearchButtonText));
                }
            }
        }

        public bool IsSearchPaused
        {
            get => _isSearchPaused;
            private set
            {
                if (_isSearchPaused != value)
                {
                    _isSearchPaused = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StopSearchButtonText));
                    OnPropertyChanged(nameof(SearchStatusText));
                    // When paused, IsSearching should still be true to show the button
                    if (value)
                    {
                        IsSearching = true;
                    }
                }
            }
        }

        public string StopSearchButtonText => _isSearchPaused ? "Continue" : "Stop";

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
                if (!IsSearching && !IsSearchPaused)
                {
                    return string.Empty;
                }
                if (IsSearchPaused)
                {
                    return $"Search paused at {Items.Count} results. Click Continue to find more.";
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
                    _activeSearchPath = null;
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
                // Clear search UI state
                ClearSearchUIState();
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
                // Clear search UI state
                ClearSearchUIState();
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
            
            // Sort existing items without re-running search
            SortCurrentItems();
        }

        private void SortCurrentItems()
        {
            var dispatcherQueue = App.MainWindow?.DispatcherQueue;
            if (dispatcherQueue == null)
            {
                return;
            }

            var currentItems = Items.ToList();
            var sortedItems = _sortDirection == SortDirection.Ascending
                ? SortItemsAscending(currentItems, _sortColumn)
                : SortItemsDescending(currentItems, _sortColumn);

            dispatcherQueue.TryEnqueue(() =>
            {
                Items.Clear();
                foreach (var item in sortedItems)
                {
                    Items.Add(item);
                }
            });
        }

        /// <summary>
        /// Cancels or continues the current search operation. Can be called from UI.
        /// </summary>
        public void CancelOrContinueSearch()
        {
            if (_isSearchPaused)
            {
                // Continue the paused search
                ContinueSearch();
            }
            else
            {
                // Cancel the search
                CancelSearch();
            }
        }

        /// <summary>
        /// Cancels the current search operation. Can be called from UI.
        /// </summary>
        public void CancelSearch()
        {
            CancelAndDisposeSearch();
            // Clear search status on UI thread
            if (App.MainWindow != null)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    IsSearching = false;
                    IsSearchPaused = false;
                    FoldersChecked = 0;
                    FilesChecked = 0;
                    _pausedSearchQueue = null;
                    _pausedProcessedDirs = null;
                });
            }
        }

        private void ContinueSearch()
        {
            if (!_isSearchPaused || _pausedSearchQueue == null || string.IsNullOrEmpty(_activeSearchPath))
            {
                return;
            }

            // Continue the search from where it paused
            var searchPath = _activeSearchPath;
            var currentSearchText = _searchText;
            
            // Create new cancellation token for continuation
            _searchCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _searchCancellationTokenSource.Token;
            
            // Clear paused state
            IsSearchPaused = false;
            IsSearching = true;

            // Continue search on background thread
            _ = Task.Run(async () =>
            {
                try
                {
                    await PerformSearchAsync(searchPath, currentSearchText, cancellationToken, 
                        _pausedSearchQueue, _pausedProcessedDirs);
                }
                catch (OperationCanceledException)
                {
                    // Search was cancelled, ignore
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Search continuation error: {ex.Message}");
                }
            }, cancellationToken);
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
            
            // Clear active search path and paused state
            _activeSearchPath = null;
            _pausedSearchQueue = null;
            _pausedProcessedDirs = null;
        }

        private void ClearSearchUIState()
        {
            // Clear search UI state on UI thread
            if (App.MainWindow != null)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    IsSearching = false;
                    IsSearchPaused = false;
                    FoldersChecked = 0;
                    FilesChecked = 0;
                });
            }
        }

        private async Task ApplySearchFilterAsync()
        {
            // Capture current path and search text at the start
            var searchPath = CurrentPath;
            var currentSearchText = _searchText;
            
            // Cancel any ongoing search
            CancelAndDisposeSearch();
            
            // Check if path is valid for search
            if (string.IsNullOrEmpty(searchPath) || !Directory.Exists(searchPath))
            {
                // At PC root or invalid path - just filter current items
                await FilterCurrentItemsAsync(currentSearchText);
                return;
            }
            
            if (string.IsNullOrWhiteSpace(currentSearchText))
            {
                // No search - show current directory items
                await ShowCurrentDirectoryItemsAsync();
                return;
            }
            
            // Start new search
            _searchCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _searchCancellationTokenSource.Token;
            _activeSearchPath = searchPath; // Track which path this search is for
            
            // Get DispatcherQueue - must be on UI thread for ObservableCollection operations
            var dispatcherQueue = App.MainWindow?.DispatcherQueue;
            if (dispatcherQueue == null)
            {
                return;
            }

            // Clear items and set searching state on UI thread
            var clearTcs = new TaskCompletionSource<bool>();
            if (!dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    Items.Clear();
                    IsSearching = true;
                    FoldersChecked = 0;
                    FilesChecked = 0;
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

            // Perform search using SearchService
            try
            {
                await PerformSearchAsync(searchPath, currentSearchText, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled, ignore
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            }
            finally
            {
                // Only update IsSearching if this search is still active (path hasn't changed)
                if (_activeSearchPath == searchPath && !cancellationToken.IsCancellationRequested)
                {
                    dispatcherQueue.TryEnqueue(() =>
                    {
                        // If not paused, set IsSearching to false
                        if (!IsSearchPaused)
                        {
                            IsSearching = false;
                        }
                    });
                }
            }
        }

        private async Task PerformSearchAsync(string searchPath, string searchTerm, CancellationToken cancellationToken, 
            Queue<string>? continuationQueue = null, HashSet<string>? continuationProcessedDirs = null)
        {
            var dispatcherQueue = App.MainWindow?.DispatcherQueue;
            if (dispatcherQueue == null)
            {
                return;
            }

            // Track items found for final sorting
            var foundItems = new List<FileSystemItem>();

            // Perform search on background thread with callback for immediate display
            var searchResult = await Task.Run(async () =>
            {
                return await _searchService.SearchAsync(
                    searchPath,
                    searchTerm,
                    (folders, files) =>
                    {
                        // Update progress on UI thread
                        if (dispatcherQueue != null && _activeSearchPath == searchPath)
                        {
                            dispatcherQueue.TryEnqueue(() =>
                            {
                                if (_activeSearchPath == searchPath) // Double-check path hasn't changed
                                {
                                    FoldersChecked = folders;
                                    FilesChecked = files;
                                }
                            });
                        }
                    },
                    (item) =>
                    {
                        // Add item to UI immediately as it's found (for progressive display)
                        if (dispatcherQueue != null && _activeSearchPath == searchPath && !cancellationToken.IsCancellationRequested)
                        {
                            foundItems.Add(item);
                            
                            // Add to UI immediately on UI thread
                            dispatcherQueue.TryEnqueue(() =>
                            {
                                // Only add if this search is still active
                                if (_activeSearchPath == searchPath && !cancellationToken.IsCancellationRequested)
                                {
                                    Items.Add(item);
                                }
                            });
                        }
                    },
                    cancellationToken,
                    MaxSearchResults,
                    continuationQueue,
                    continuationProcessedDirs);
            }, cancellationToken);

            // Check if search was paused
            if (searchResult.IsPaused && _activeSearchPath == searchPath && !cancellationToken.IsCancellationRequested)
            {
                dispatcherQueue.TryEnqueue(() =>
                {
                    if (_activeSearchPath == searchPath)
                    {
                        IsSearchPaused = true;
                        IsSearching = true; // Keep searching state visible
                        _pausedSearchQueue = searchResult.RemainingQueue;
                        _pausedProcessedDirs = searchResult.ProcessedDirectories;
                    }
                });
            }
        }

        private async Task FilterCurrentItemsAsync(string searchText)
        {
            var dispatcherQueue = App.MainWindow?.DispatcherQueue;
            if (dispatcherQueue == null)
            {
                return;
            }

            var filteredItems = new List<FileSystemItem>();
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                filteredItems.AddRange(_allItems);
            }
            else
            {
                var searchLower = searchText.ToLowerInvariant();
                filteredItems = _allItems.Where(item => 
                    item.Name.ToLowerInvariant().Contains(searchLower)).ToList();
            }

            var sortedItems = _sortDirection == SortDirection.Ascending
                ? SortItemsAscending(filteredItems, _sortColumn)
                : SortItemsDescending(filteredItems, _sortColumn);

            var addTcs = new TaskCompletionSource<bool>();
            if (!dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    Items.Clear();
                    foreach (var item in sortedItems)
                    {
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
                return;
            }
            await addTcs.Task;
        }

        private async Task ShowCurrentDirectoryItemsAsync()
        {
            var dispatcherQueue = App.MainWindow?.DispatcherQueue;
            if (dispatcherQueue == null)
            {
                return;
            }

            var sortedItems = _sortDirection == SortDirection.Ascending
                ? SortItemsAscending(_allItems, _sortColumn)
                : SortItemsDescending(_allItems, _sortColumn);

            var addTcs = new TaskCompletionSource<bool>();
            if (!dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    Items.Clear();
                    foreach (var item in sortedItems)
                    {
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
                return;
            }
            await addTcs.Task;
        }

        private void ApplySearchFilter()
        {
            // Synchronous version for immediate updates (e.g., when clearing search)
            _ = ApplySearchFilterAsync();
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

