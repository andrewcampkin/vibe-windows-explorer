using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WindowsExplorer.Services;

namespace WindowsExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for managing multiple tabs in the file explorer.
    /// </summary>
    public class TabManagerViewModel : INotifyPropertyChanged
    {
        private TabViewModel? _selectedTab;

        public TabManagerViewModel()
        {
            Tabs = new ObservableCollection<TabViewModel>();
            // Create initial tab
            CreateNewTab();
        }

        public ObservableCollection<TabViewModel> Tabs { get; }

        public TabViewModel? SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab != value)
                {
                    if (_selectedTab != null)
                    {
                        _selectedTab.IsSelected = false;
                    }
                    _selectedTab = value;
                    if (_selectedTab != null)
                    {
                        _selectedTab.IsSelected = true;
                    }
                    OnPropertyChanged();
                }
            }
        }

        public TabViewModel CreateNewTab()
        {
            var newTab = new TabViewModel();
            Tabs.Add(newTab);
            SelectedTab = newTab;
            return newTab;
        }

        public void CloseTab(TabViewModel tab)
        {
            if (Tabs.Count <= 1)
            {
                // Don't close the last tab, just reset it
                tab.ExplorerViewModel.NavigateToPath(string.Empty);
                return;
            }

            var index = Tabs.IndexOf(tab);
            Tabs.Remove(tab);

            // Select another tab if the closed one was selected
            if (SelectedTab == tab)
            {
                if (index > 0)
                {
                    SelectedTab = Tabs[index - 1];
                }
                else if (Tabs.Count > 0)
                {
                    SelectedTab = Tabs[0];
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

