using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.Graphics;
using Microsoft.UI.Windowing;
using Windows.ApplicationModel;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Diagnostics;
using WindowsExplorer.Models;
using WindowsExplorer.ViewModels;

namespace WindowsExplorer
{
    /// <summary>
    /// Main window for the Windows Explorer application.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public TabManagerViewModel TabManager { get; }
        private FileExplorerViewModel? _currentViewModel;

        public MainWindow()
        {
            InitializeComponent();
            
            // Customize title bar
            try
            {
                var packagePath = Package.Current.InstalledLocation.Path;
                var iconPath = System.IO.Path.Combine(packagePath, "Assets", "Square44x44Logo.png");
                if (System.IO.File.Exists(iconPath))
                {
                    AppWindow.SetIcon(iconPath);
                }
            }
            catch
            {
                // Icon setting failed, continue without it
            }
            AppWindow.Title = "Windows Explorer";
            
            // Set window size to approximately 1/4 of screen
            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            var screenWidth = displayArea.WorkArea.Width;
            var screenHeight = displayArea.WorkArea.Height;
            
            // Set window to about 1/4 of screen (slightly larger for better usability)
            var windowWidth = (int)(screenWidth * 0.4);
            var windowHeight = (int)(screenHeight * 0.5);
            
            // Center the window
            var windowX = (screenWidth - windowWidth) / 2;
            var windowY = (screenHeight - windowHeight) / 2;
            
            AppWindow.Resize(new SizeInt32(windowWidth, windowHeight));
            AppWindow.Move(new PointInt32(windowX, windowY));
            
            // Extend title bar into client area
            ExtendTitleBar();
            
            TabManager = new TabManagerViewModel();
            var rootGrid = (Grid)this.Content;
            rootGrid.DataContext = TabManager;
            
            // Set up tab view - TabView doesn't use ItemsSource, we manage tabs manually
            TabManager.PropertyChanged += TabManager_PropertyChanged;
            TabManager.Tabs.CollectionChanged += Tabs_CollectionChanged;
            
            // Add initial tab to TabView
            if (TabManager.SelectedTab != null)
            {
                AddTabToView(TabManager.SelectedTab);
                SetCurrentViewModel(TabManager.SelectedTab.ExplorerViewModel);
            }
            
            // Set up keyboard shortcuts on the root Grid
            rootGrid.KeyDown += MainWindow_KeyDown;
        }

        private void ExtendTitleBar()
        {
            if (AppWindow.TitleBar != null)
            {
                // Extend title bar into client area
                AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                
                // Set the drag region - the entire title bar grid except window controls
                TitleBarGrid.Loaded += (s, e) =>
                {
                    SetDragRegion();
                };
                
                // Update drag region when window is resized
                AppWindow.Changed += (s, e) =>
                {
                    if (e.DidSizeChange)
                    {
                        SetDragRegion();
                    }
                };
            }
        }

        private void SetDragRegion()
        {
            if (AppWindow.TitleBar == null || TitleBarGrid == null) return;
            
            // Calculate the drag region (entire title bar except window controls area)
            var scaleAdjustment = TitleBarGrid.XamlRoot?.RasterizationScale ?? 1.0;
            var windowControlsWidth = WindowControlsGrid.ActualWidth * scaleAdjustment;
            
            var dragRegion = new Windows.Graphics.RectInt32
            {
                X = 0,
                Y = 0,
                Width = (int)((TitleBarGrid.ActualWidth - windowControlsWidth) * scaleAdjustment),
                Height = (int)(TitleBarGrid.ActualHeight * scaleAdjustment)
            };
            
            AppWindow.TitleBar.SetDragRectangles(new Windows.Graphics.RectInt32[] { dragRegion });
        }

        private void SetCurrentViewModel(FileExplorerViewModel viewModel)
        {
            // Unsubscribe from previous viewmodel
            if (_currentViewModel != null)
            {
                _currentViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            
            _currentViewModel = viewModel;
            _currentViewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // Update UI
            UpdateUIFromViewModel();
        }

        private void Tabs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (TabViewModel newTab in e.NewItems)
                {
                    AddTabToView(newTab);
                }
            }
            
            if (e.OldItems != null)
            {
                foreach (TabViewModel oldTab in e.OldItems)
                {
                    RemoveTabFromView(oldTab);
                }
            }
        }

        private void AddTabToView(TabViewModel tabViewModel)
        {
            var tabViewItem = new TabViewItem
            {
                Header = tabViewModel.Title,
                Content = null, // Content is handled by the main content area
                Tag = tabViewModel // Store the ViewModel in Tag for easy access
            };
            
            // Update title when it changes
            tabViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TabViewModel.Title))
                {
                    tabViewItem.Header = tabViewModel.Title;
                }
            };
            
            TabViewControl.TabItems.Add(tabViewItem);
            
            if (tabViewModel.IsSelected)
            {
                TabViewControl.SelectedItem = tabViewItem;
            }
        }

        private void RemoveTabFromView(TabViewModel tabViewModel)
        {
            for (int i = TabViewControl.TabItems.Count - 1; i >= 0; i--)
            {
                if (TabViewControl.TabItems[i] is TabViewItem item && item.Tag == tabViewModel)
                {
                    TabViewControl.TabItems.RemoveAt(i);
                    break;
                }
            }
        }

        private void TabManager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TabManager.SelectedTab))
            {
                // Find and select the corresponding TabViewItem
                foreach (TabViewItem item in TabViewControl.TabItems)
                {
                    if (item.Tag == TabManager.SelectedTab)
                    {
                        TabViewControl.SelectedItem = item;
                        break;
                    }
                }
                
                if (TabManager.SelectedTab != null)
                {
                    SetCurrentViewModel(TabManager.SelectedTab.ExplorerViewModel);
                }
            }
        }

        private void TabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabViewControl.SelectedItem is TabViewItem selectedItem && selectedItem.Tag is TabViewModel selectedTab)
            {
                if (selectedTab != TabManager.SelectedTab)
                {
                    TabManager.SelectedTab = selectedTab;
                }
            }
        }

        private void TabView_AddTabButtonClick(TabView sender, object args)
        {
            TabManager.CreateNewTab();
        }

        private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Tab is TabViewItem tabItem && tabItem.Tag is TabViewModel tab)
            {
                TabManager.CloseTab(tab);
            }
        }

        private void UpdateUIFromViewModel()
        {
            if (_currentViewModel == null) return;
            
            // Initialize address bar with current path
            AddressTextBox.Text = string.IsNullOrEmpty(_currentViewModel.CurrentPath) ? "This PC" : _currentViewModel.CurrentPath;
            
            // Initialize button states
            BackButton.IsEnabled = _currentViewModel.CanGoBack;
            ForwardButton.IsEnabled = _currentViewModel.CanGoForward;
            UpButton.IsEnabled = _currentViewModel.CanGoUp;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_currentViewModel == null) return;
            
            // Update UI when ViewModel properties change
            if (e.PropertyName == nameof(FileExplorerViewModel.CurrentPath))
            {
                // Update address bar when path changes
                // Show "This PC" when at root (empty path)
                AddressTextBox.Text = string.IsNullOrEmpty(_currentViewModel.CurrentPath) ? "This PC" : _currentViewModel.CurrentPath;
            }
            else if (e.PropertyName == nameof(FileExplorerViewModel.CanGoBack))
            {
                BackButton.IsEnabled = _currentViewModel.CanGoBack;
            }
            else if (e.PropertyName == nameof(FileExplorerViewModel.CanGoForward))
            {
                ForwardButton.IsEnabled = _currentViewModel.CanGoForward;
            }
            else if (e.PropertyName == nameof(FileExplorerViewModel.CanGoUp))
            {
                UpButton.IsEnabled = _currentViewModel.CanGoUp;
            }
            else if (e.PropertyName == nameof(FileExplorerViewModel.SearchText))
            {
                // Update search textbox when search text changes (e.g., when navigating)
                if (SearchTextBox.Text != _currentViewModel.SearchText)
                {
                    SearchTextBox.Text = _currentViewModel.SearchText;
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _currentViewModel?.NavigateBack();
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            _currentViewModel?.NavigateForward();
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            _currentViewModel?.NavigateUp();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && _currentViewModel != null)
            {
                // Use the actual text from the TextBox to ensure we have the latest value
                // TextChanged fires during the change, so we need to get the current Text value
                var currentText = textBox.Text ?? string.Empty;
                if (_currentViewModel.SearchText != currentText)
                {
                    _currentViewModel.SearchText = currentText;
                }
            }
        }

        private void AddressTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                NavigateToAddress();
            }
        }

        private void AddressTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            NavigateToAddress();
        }

        private void NavigateToAddress()
        {
            if (_currentViewModel == null) return;
            
            var path = AddressTextBox.Text;
            // Handle "This PC" as root path
            if (path == "This PC" || string.IsNullOrWhiteSpace(path))
            {
                _currentViewModel.NavigateToPath(string.Empty);
            }
            else if (!string.IsNullOrEmpty(path))
            {
                _currentViewModel.NavigateToPath(path);
            }
            else
            {
                // Reset to current path if empty
                AddressTextBox.Text = string.IsNullOrEmpty(_currentViewModel.CurrentPath) ? "This PC" : _currentViewModel.CurrentPath;
            }
        }

        private void FileListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileSystemItem item && _currentViewModel != null)
            {
                if (item.IsDirectory)
                {
                    _currentViewModel.NavigateToItem(item);
                }
                else
                {
                    // If item has a parent path that's different from current (from search), navigate to folder
                    if (!string.IsNullOrEmpty(item.ParentPath) && 
                        !string.IsNullOrEmpty(_currentViewModel.CurrentPath) &&
                        !item.ParentPath.Equals(_currentViewModel.CurrentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // Navigate to the folder containing the file
                        _currentViewModel.NavigateToPath(item.ParentPath);
                    }
                    else
                    {
                        // Open file with default application
                        OpenFile(item.Path);
                    }
                }
            }
        }

        private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileListView.SelectedItem is FileSystemItem item && !item.IsDirectory)
            {
                // Could add preview functionality here later
            }
        }

        private async void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var modifiers = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
            bool altPressed = (modifiers & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

            if (_currentViewModel == null) return;
            
            if (altPressed)
            {
                switch (e.Key)
                {
                    case VirtualKey.Left:
                        if (_currentViewModel.CanGoBack)
                        {
                            _currentViewModel.NavigateBack();
                            e.Handled = true;
                        }
                        break;
                    case VirtualKey.Right:
                        if (_currentViewModel.CanGoForward)
                        {
                            _currentViewModel.NavigateForward();
                            e.Handled = true;
                        }
                        break;
                    case VirtualKey.Up:
                        if (_currentViewModel.CanGoUp)
                        {
                            _currentViewModel.NavigateUp();
                            e.Handled = true;
                        }
                        break;
                }
            }
            else if (e.Key == VirtualKey.Enter)
            {
                if (FileListView.SelectedItem is FileSystemItem selectedItem)
                {
                    if (selectedItem.IsDirectory)
                    {
                        _currentViewModel.NavigateToItem(selectedItem);
                    }
                    else
                    {
                        // Open file with default application
                        OpenFile(selectedItem.Path);
                    }
                    e.Handled = true;
                }
                else
                {
                    // Focus address bar and navigate
                    AddressTextBox.Focus(FocusState.Programmatic);
                    NavigateToAddress();
                    e.Handled = true;
                }
            }
            else
            {
                // Handle Ctrl+C, Ctrl+X, Ctrl+V
                var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
                bool ctrlPressed = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
                
                if (ctrlPressed)
                {
                    switch (e.Key)
                    {
                        case VirtualKey.C:
                            await CopySelectedItems();
                            e.Handled = true;
                            break;
                        case VirtualKey.X:
                            await CutSelectedItems();
                            e.Handled = true;
                            break;
                        case VirtualKey.V:
                            await PasteItems();
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task CopySelectedItems()
        {
            if (_currentViewModel == null) return;
            
            var selectedItems = FileListView.SelectedItems.Cast<FileSystemItem>().ToList();
            if (selectedItems.Count > 0)
            {
                await _currentViewModel.CopySelectedItemsAsync(selectedItems);
            }
        }

        private async System.Threading.Tasks.Task CutSelectedItems()
        {
            if (_currentViewModel == null) return;
            
            var selectedItems = FileListView.SelectedItems.Cast<FileSystemItem>().ToList();
            if (selectedItems.Count > 0)
            {
                await _currentViewModel.CutSelectedItemsAsync(selectedItems);
            }
        }

        private async System.Threading.Tasks.Task PasteItems()
        {
            if (_currentViewModel == null) return;
            
            await _currentViewModel.PasteAsync();
        }

        private void FileListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // Select the item that was right-clicked
            if (e.OriginalSource is FrameworkElement element && element.DataContext is FileSystemItem item)
            {
                FileListView.SelectedItem = item;
            }
        }

        private void FileListContextMenu_Opening(object sender, object e)
        {
            // Update context menu items based on selection and clipboard state
            bool hasSelection = FileListView.SelectedItems.Count > 0;
            bool canPaste = _currentViewModel?.CanPaste ?? false;
            bool isFile = FileListView.SelectedItem is FileSystemItem item && !item.IsDirectory;
            bool hasParentPath = FileListView.SelectedItem is FileSystemItem selectedItem && 
                                !string.IsNullOrEmpty(selectedItem.ParentPath);
            
            if (ContextMenu_Open != null)
            {
                ContextMenu_Open.IsEnabled = hasSelection && isFile;
            }
            if (ContextMenu_OpenFileLocation != null)
            {
                ContextMenu_OpenFileLocation.IsEnabled = hasSelection && hasParentPath;
            }
            if (ContextMenu_OpenFileLocationNewTab != null)
            {
                ContextMenu_OpenFileLocationNewTab.IsEnabled = hasSelection && hasParentPath;
            }
            if (ContextMenu_Copy != null)
            {
                ContextMenu_Copy.IsEnabled = hasSelection;
            }
            if (ContextMenu_Cut != null)
            {
                ContextMenu_Cut.IsEnabled = hasSelection;
            }
            if (ContextMenu_Paste != null)
            {
                ContextMenu_Paste.IsEnabled = canPaste;
            }
        }

        private void ContextMenu_Open_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileSystemItem item && !item.IsDirectory)
            {
                OpenFile(item.Path);
            }
        }

        private void ContextMenu_OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileSystemItem item && 
                !string.IsNullOrEmpty(item.ParentPath) && 
                _currentViewModel != null)
            {
                // Navigate to the parent directory
                _currentViewModel.NavigateToPath(item.ParentPath);
            }
        }

        private void ContextMenu_OpenFileLocationNewTab_Click(object sender, RoutedEventArgs e)
        {
            if (FileListView.SelectedItem is FileSystemItem item && 
                !string.IsNullOrEmpty(item.ParentPath))
            {
                // Create a new tab and navigate to the parent directory
                var newTab = TabManager.CreateNewTab();
                newTab.ExplorerViewModel.NavigateToPath(item.ParentPath);
            }
        }

        private void OpenFile(string filePath)
        {
            try
            {
                // Use Process.Start with UseShellExecute to open file with default application
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                };
                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                // Could show an error message to user
                System.Diagnostics.Debug.WriteLine($"Error opening file {filePath}: {ex.Message}");
            }
        }

        private async void ContextMenu_Copy_Click(object sender, RoutedEventArgs e)
        {
            await CopySelectedItems();
        }

        private async void ContextMenu_Cut_Click(object sender, RoutedEventArgs e)
        {
            await CutSelectedItems();
        }

        private async void ContextMenu_Paste_Click(object sender, RoutedEventArgs e)
        {
            await PasteItems();
        }

        private void NameHeader_Click(object sender, RoutedEventArgs e)
        {
            _currentViewModel?.SortBy(ViewModels.SortColumn.Name);
        }

        private void SizeHeader_Click(object sender, RoutedEventArgs e)
        {
            _currentViewModel?.SortBy(ViewModels.SortColumn.Size);
        }

        private void TypeHeader_Click(object sender, RoutedEventArgs e)
        {
            _currentViewModel?.SortBy(ViewModels.SortColumn.Type);
        }

        private void DateModifiedHeader_Click(object sender, RoutedEventArgs e)
        {
            _currentViewModel?.SortBy(ViewModels.SortColumn.DateModified);
        }

    }
}
