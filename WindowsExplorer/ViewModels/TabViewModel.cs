using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WindowsExplorer.Services;

namespace WindowsExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for a single tab in the file explorer.
    /// </summary>
    public class TabViewModel : INotifyPropertyChanged
    {
        private string _title = "New Tab";
        private bool _isSelected;

        public TabViewModel()
        {
            ExplorerViewModel = new FileExplorerViewModel();
            ExplorerViewModel.PropertyChanged += ExplorerViewModel_PropertyChanged;
            UpdateTitle();
        }

        public FileExplorerViewModel ExplorerViewModel { get; }

        public string Title
        {
            get => _title;
            private set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        private void ExplorerViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExplorerViewModel.CurrentPath))
            {
                UpdateTitle();
            }
        }

        private void UpdateTitle()
        {
            var path = ExplorerViewModel.CurrentPath;
            if (string.IsNullOrEmpty(path))
            {
                Title = "This PC";
            }
            else
            {
                // Get the folder name or drive letter
                try
                {
                    var dirInfo = new System.IO.DirectoryInfo(path);
                    Title = dirInfo.Name;
                    if (string.IsNullOrEmpty(Title))
                    {
                        // If name is empty, it's a drive root
                        Title = path.TrimEnd('\\');
                    }
                }
                catch
                {
                    Title = path;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

