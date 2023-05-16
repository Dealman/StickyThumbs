using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls;
using ControlzEx.Theming;
using System.Linq;
using MahApps.Metro.Controls.Dialogs;
using System.Reflection;

namespace StickyThumbs
{
    public partial class MainWindow : MetroWindow
    {
        ObservableCollection<Theme> Themes = new ObservableCollection<Theme>(ThemeManager.Current.Themes.Where(x => x.Name.Contains("Dark")));
        ObservableCollection<UserControls.ProcessControl> ProcessControlCollection = new();
        Dictionary<string, Windows.ThumbnailWindow> ThumbnailDictionary = new();

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            ThemesComboBox.SelectionChanged += ThemesComboBox_SelectionChanged;
            DarkModeSwitch.Toggled += DarkModeSwitch_Toggled;
            ProcessDataGrid.MouseDoubleClick += ProcessDataGrid_MouseDoubleClick;
            BlacklistDataGrid.MouseDoubleClick += BlacklistDataGrid_MouseDoubleClick;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ThumbnailDictionary.Count > 0 && CloseThumbnailsCheckBox.IsChecked.GetValueOrDefault())
            {
                foreach(var thumbnail in ThumbnailDictionary.Values)
                {
                    if (thumbnail is not null)
                        thumbnail.Close();
                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            #region Load Previous Application State (Theme)
            ThemeManager.Current.ChangeTheme(this, Properties.Settings.Default.AppTheme);
            DarkModeSwitch.IsOn = ThemeManager.Current.DetectTheme(this).Name.Contains("Dark") ? true : false;
            #endregion

            #region Initialize Theme Selector
            ThemesComboBox.ItemsSource = Themes;
            var currentTheme = ThemeManager.Current.DetectTheme(this);
            if (currentTheme is not null)
                ThemesComboBox.SelectedValue = Themes.Where(x => x.ColorScheme == currentTheme.ColorScheme).First();
            #endregion

            UpdateBlacklistDataGrid();
            RefreshProcessCollection();
        }

        private void SaveSetting(string settingName)
        {
            switch (settingName)
            {
                case "AppTheme":
                    var appTheme = ThemeManager.Current.DetectTheme(this)?.Name ?? "Dark.Blue";
                    Properties.Settings.Default.AppTheme = appTheme;
                    break;
                case "CloseThumbnails":
                    Properties.Settings.Default.CloseThumbnails = CloseThumbnailsCheckBox.IsChecked.GetValueOrDefault();
                    break;
            }

            Properties.Settings.Default.Save();
        }

        #region Theme Events
        private void DarkModeSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (DarkModeSwitch.IsLoaded)
            {
                var theme = ThemesComboBox.SelectedItem as Theme;
                if (theme is null)
                    return;

                if (DarkModeSwitch.IsOn)
                    ThemeManager.Current.ChangeTheme(this, theme.Name.Replace("Light.", "Dark."));
                else
                    ThemeManager.Current.ChangeTheme(this, theme.Name.Replace("Dark.", "Light."));

                SaveSetting("AppTheme");
            }
        }
        private void ThemesComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ThemesComboBox.IsLoaded)
            {
                var theme = ThemesComboBox.SelectedItem as Theme;
                if (theme is null)
                    return;

                ThemeManager.Current.ChangeThemeColorScheme(this, theme.Name.Replace("Dark.", ""));
                if (ThumbnailDictionary.Count > 0)
                {
                    foreach (var thumbnailWindow in ThumbnailDictionary)
                    {
                        ThemeManager.Current.ChangeThemeColorScheme(thumbnailWindow.Value, theme.Name.Replace("Dark.", ""));
                    }
                }
                SaveSetting("AppTheme");
            }
        }
        #endregion

        #region DataGrid Events & Methods
        private void BlacklistDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selectedItem = BlacklistDataGrid.SelectedItem;
            if (selectedItem is null)
                return;

            if (MessageBox.Show($"Are you sure you want to remove \"{selectedItem}\" from the Blacklist?", "Are You Sure?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Blacklist.Remove((string)selectedItem);
                UpdateBlacklistDataGrid();
            }
        }
        private void ProcessDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selectedProcess = ProcessDataGrid.SelectedItem as UserControls.ProcessControl;
            if (selectedProcess is not null)
            {
                if (ThumbnailDictionary.ContainsKey(selectedProcess.Process.ProcessName))
                    return;

                try
                {
                    Windows.ThumbnailWindow thumbnailWindow = new Windows.ThumbnailWindow(selectedProcess.Process);
                    var appTheme = ThemeManager.Current.DetectTheme(this)?.Name ?? "Dark.Blue";
                    ThemeManager.Current.ChangeTheme(thumbnailWindow, appTheme);
                    thumbnailWindow.Closing += ThumbnailWindow_Closing;
                    ThumbnailDictionary.Add(selectedProcess.Process.ProcessName, thumbnailWindow);
                    thumbnailWindow.Show();
                    selectedProcess.Process.EnableRaisingEvents = true;
                    selectedProcess.Process.Exited += Process_Exited;
                }
                catch
                {
                    // TODO: Error/Warning message, likely System.ComponentModel.Win32Exception
                }
            }
        }
        void UpdateBlacklistDataGrid()
        {
            if (BlacklistDataGrid.Items.Count > 0)
                BlacklistDataGrid.Items.Clear();

            Blacklist.Load();
            var black = Blacklist.GetBlacklist();
            black.ForEach(x => BlacklistDataGrid.Items.Add(x));
            BlacklistDataGrid.Items.Refresh();
        }
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshProcessCollection();
        }
        void RefreshProcessCollection()
        {
            if (ProcessControlCollection.Count > 0)
                ProcessControlCollection.Clear();

            Process[] processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                if (process.MainWindowHandle != IntPtr.Zero && !Blacklist.Contains(process.ProcessName))
                {
                    UserControls.ProcessControl processControl = new UserControls.ProcessControl(process);
                    ProcessControlCollection.Add(processControl);
                }
            }

            ProcessDataGrid.ItemsSource = ProcessControlCollection;
        }
        #endregion

        #region Thumbnail Events
        private void Process_Exited(object? sender, EventArgs e)
        {
            // TODO: Needs some more testing, probably use Try/Catch
            foreach (KeyValuePair<string, Windows.ThumbnailWindow> keyValue in ThumbnailDictionary)
            {
                if (keyValue.Value.MonitoredProcess == ((Process)sender))
                {
                    App.Current.Dispatcher.Invoke(keyValue.Value.Close);
                    return;
                }
            }
        }
        private void ThumbnailWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            ThumbnailDictionary.Remove(((Windows.ThumbnailWindow)sender).MonitoredProcess.ProcessName);
        }
        #endregion

        private async void WindowButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender == GitHubButton)
            {
                Process.Start(new ProcessStartInfo{FileName = "https://www.github.com/Dealman/StickyThumbs", UseShellExecute = true});
            }

            if (sender == AboutButton)
            {
                await this.ShowMessageAsync($"StickyThumbs V{Assembly.GetExecutingAssembly().GetName().Version.ToString()}", "Creates resizeable, zoomable 'sticky' thumbnails which remain on top of other windows.\n\nIcons created by https://www.flaticon.com/authors/freepik");
            }
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender == CloseThumbnailsCheckBox && CloseThumbnailsCheckBox.IsLoaded)
            {
                SaveSetting("CloseThumbnails");
            }
        }
    }
}
