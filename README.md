# Windows Explorer Replacement

A modern Windows file explorer application built with C# and WinUI 3.

## Features

- **PC Root View**: Opens at the PC root level showing all available drives by default
- **Folder Navigation**: Navigate through Windows folders with back, forward, and up buttons
- **File Display**: View files and folders in a detailed list with columns for Name, Size, Type, and Date Modified
- **Column Sorting**: Click any column header to sort by that column. Click again to toggle ascending/descending order. Directories are always shown before files.
- **Search/Filter**: Real-time filtering of files and folders by name (case-insensitive contains search)
- **Address Bar**: Type or edit paths directly in the address bar. Supports typing drive letters (e.g., "C:\") or full paths
- **Keyboard Shortcuts**: 
  - `Alt + Left Arrow`: Navigate back
  - `Alt + Right Arrow`: Navigate forward
  - `Alt + Up Arrow`: Navigate up
  - `Enter`: Open selected folder or navigate to address bar path
- **Double-Click**: Double-click folders to open them

## Requirements

- Windows 10 version 1809 (build 17763) or later
- .NET 8.0 or later
- Windows App SDK 1.8 or later
- **Visual Studio 2022** (required - this project can only be run from Visual Studio)

## Building and Running

**Note: This application can only be run from Visual Studio 2022. It cannot be run from the command line or as a standalone executable.**

1. Open `WindowsExplorer.sln` in Visual Studio 2022
2. Restore NuGet packages (Visual Studio will do this automatically, or use Tools > NuGet Package Manager > Restore NuGet Packages)
3. Build the solution (F6 or Build > Build Solution)
4. Run the application (F5 or Debug > Start Debugging)

## Project Structure

```
WindowsExplorer/
├── Models/              # Data models (FileSystemItem)
├── Services/            # File system operations (FileSystemService)
├── ViewModels/          # MVVM view models (FileExplorerViewModel with sorting and search)
├── Converters/          # Value converters for UI binding (FileSize, Date, Symbol)
├── Assets/              # Application icons and splash screens
├── MainWindow.xaml      # Main UI window with navigation, search, and file list
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
- **Navigation**: Full history support with back/forward navigation
- **Search**: Real-time filtering that works with current sort order
- **Sorting**: All columns (Name, Size, Type, Date Modified) are sortable with visual indicators
- **Window Size**: Automatically sized to approximately 40% width and 50% height of screen, centered

## Future Enhancements

- File operations (copy, move, delete, rename)
- Multiple tabs
- Preview pane
- Customizable views (details, tiles, icons)
- Context menus (right-click)
- Drag and drop
- File properties dialog
- Thumbnail view for images

