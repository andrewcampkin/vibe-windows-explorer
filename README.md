# Windows Explorer Replacement

A modern Windows file explorer application built with C# and WinUI 3.

## Features

- **PC Root View**: Opens at the PC root level showing all available drives by default
- **Multiple Tabs**: Open multiple file explorer tabs for easy navigation between different locations
- **Folder Navigation**: Navigate through Windows folders with back, forward, and up buttons
- **File Display**: View files and folders in a detailed list with columns for Name, Size, Type, and Date Modified
- **Column Sorting**: Click any column header to sort by that column. Click again to toggle ascending/descending order. Directories are always shown before files.
- **Search/Filter**: 
  - Real-time filtering of files and folders by name (case-insensitive contains search) in the current directory
  - Deep search functionality to search across subdirectories with progress tracking
- **Address Bar**: Type or edit paths directly in the address bar. Supports typing drive letters (e.g., "C:\") or full paths
- **Context Menus**: Right-click on files and folders for context menu options:
  - Open / Open File Location
  - Copy / Cut / Paste
- **Clipboard Operations**: Copy, cut, and paste files and folders between locations
- **Keyboard Shortcuts**: 
  - `Alt + Left Arrow`: Navigate back
  - `Alt + Right Arrow`: Navigate forward
  - `Alt + Up Arrow`: Navigate up
  - `Enter`: Open selected folder or navigate to address bar path
- **Double-Click**: Double-click folders to open them

## Requirements

- Windows 10 version 1809 (build 17763) or later
- .NET 8.0 SDK or later
- Windows App SDK 1.8 or later
- **Visual Studio 2022**

## Building and Running

1. Open `WindowsExplorer.sln` in Visual Studio 2022
2. Press **F5** to build and run (or Build > Build Solution, then Debug > Start Debugging)

### Publishing

Publish as a self-contained executable that runs on any Windows 10+ machine without requiring .NET:

1. Right-click the `WindowsExplorer` project → **Publish**
2. Select a publish profile: **win-x64**, **win-x86**, or **win-arm64**
3. Click **Publish**

Output location: `\bin\win-x64\publish` or similar

Copy the entire `publish` folder to run `WindowsExplorer.exe` on any Windows machine or just run from that folder.

## Project Structure

```
WindowsExplorer/
├── Models/              # Data models (FileSystemItem)
├── Services/            # Business logic services
│   ├── FileSystemService.cs    # File system operations
│   ├── SearchService.cs        # Search functionality
│   └── ClipboardService.cs     # Clipboard operations (copy, cut, paste)
├── ViewModels/          # MVVM view models
│   ├── FileExplorerViewModel.cs  # Main file explorer logic with sorting and search
│   ├── TabManagerViewModel.cs    # Tab management
│   └── TabViewModel.cs           # Individual tab view model
├── Converters/          # Value converters for UI binding
│   ├── FileSizeConverter.cs
│   ├── DateConverter.cs
│   ├── SymbolConverter.cs
│   └── BooleanToVisibilityConverter.cs
├── Assets/              # Application icons and splash screens
├── MainWindow.xaml      # Main UI window with tabs, navigation, search, and file list
├── MainWindow.xaml.cs   # Code-behind for MainWindow
├── App.xaml             # Application entry point
└── App.xaml.cs          # Application initialization
```

## Technology Stack

- **C#** (.NET 8)
- **WinUI 3** (Microsoft's modern Windows UI framework)
- **Windows App SDK 1.8** (for Windows integration)
- **MVVM Pattern** (Model-View-ViewModel architecture)

## Current Implementation Details

- **Default View**: Opens at PC root showing all drives (C:\, D:\, etc.)
- **Navigation**: Full history support with back/forward navigation per tab
- **Tabs**: Multiple tabs with independent navigation history and search state
- **Search**: 
  - Real-time filtering in current directory that works with current sort order
  - Deep search across subdirectories with progress tracking and cancellation
- **Sorting**: All columns (Name, Size, Type, Date Modified) are sortable with visual indicators
- **File Operations**: Copy, cut, and paste operations via context menu or keyboard shortcuts
- **Context Menus**: Right-click context menus for files and folders
- **Window Size**: Automatically sized to approximately 40% width and 50% height of screen, centered
- **UI**: Modern WinUI 3 interface with Mica backdrop

## Future Enhancements

- File operations (delete, rename)
- Preview pane
- Customizable views (details, tiles, icons)
- Drag and drop
- File properties dialog
- Thumbnail view for images
