using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Forms;
using ClearView.Data;
using ClearView.ViewModel;
using ClearView.Utils;
using System.Linq;

namespace ClearView.UI
{
    public partial class LauncherWindow : Window
    {
        private readonly LauncherViewModel _viewModel;
        private const string PlaceholderText = "Search files, apps, and more...";
        private bool _isProgrammaticallyChangingText = false;

        // CHANGED: These are now used again.
        private NotifyIcon? _notifyIcon;
        private HotkeyHelper? _hotkeyHelper;

        public LauncherWindow()
        {
            InitializeComponent();
            _viewModel = new LauncherViewModel();
            DataContext = _viewModel;
            SetPlaceholder();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e) // CHANGED: Restored full logic
        {
            try
            {
                WindowAccent.EnableBlur(this);
                SetupSystemTrayIcon();
                SetupHotkey();
            
                _viewModel.UpdateDefaultView();
                FocusSearchBox();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"A critical error occurred on startup: {ex.Message}\n\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void SetupSystemTrayIcon() // CHANGED: Restored full logic
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/Assets/search_icon.ico", UriKind.RelativeOrAbsolute);
                using (var iconStream = System.Windows.Application.GetResourceStream(iconUri)?.Stream)
                {
                    if (iconStream != null)
                    {
                        _notifyIcon = new NotifyIcon
                        {
                            Icon = new System.Drawing.Icon(iconStream),
                            Visible = true,
                            Text = "ClearView"
                        };
                        _notifyIcon.Click += (s, args) => ShowLauncher();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not create tray icon: {ex.Message}");
            }
        }

        private void SetupHotkey() // CHANGED: Restored full logic
        {
            _hotkeyHelper?.Unregister();
            _hotkeyHelper = new HotkeyHelper(this, _viewModel.GeneralSettings);
            _hotkeyHelper.Register();
        }

        public void ShowLauncher()
        {
            if (this.IsVisible)
            {
                this.Hide();
            }
            else
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
                this.Show();
                this.Activate();
                SearchBox.Clear();
                _viewModel.UpdateDefaultView();
                FocusSearchBox();
            }
        }
        
        private async void OpenSettingsWindow()
        {
            var settingsWindow = new SettingsWindow(new List<string>(_viewModel.SearchScope), _viewModel.IndexingSettings, _viewModel.ExclusionSettings, _viewModel.GeneralSettings);
            if (settingsWindow.ShowDialog() == true)
            {
                _viewModel.SearchScope = settingsWindow.SelectedDrives;
                _viewModel.IndexingSettings = settingsWindow.IndexingSettings;
                _viewModel.ExclusionSettings = settingsWindow.ExclusionSettings;
                _viewModel.GeneralSettings = settingsWindow.GeneralSettings;
                
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearView");
                File.WriteAllText(Path.Combine(appDataPath, "drives.json"), System.Text.Json.JsonSerializer.Serialize(_viewModel.SearchScope));
                _viewModel.SaveIndexingSettings();
                _viewModel.SaveExclusionSettings();
                _viewModel.SaveGeneralSettings();

                if (settingsWindow.HotkeyChanged)
                {
                    SetupHotkey();
                    var messageWindow = new MessageWindow("Hotkey Changed", "Please restart ClearView for the new hotkey to take effect.") { Owner = this };
                    messageWindow.ShowDialog();
                }

                if (settingsWindow.ShouldRebuildIndex)
                {
                    await _viewModel.BuildFileSystemIndexAsync(true);
                }
            }
        }

        private void LaunchItem(SearchResult item) // CHANGED: Restored full logic to handle all cases
        {
            if (item == null) return;
        
            if (item.Type == ResultType.Calculator)
            {
                try
                {
                    System.Windows.Clipboard.SetText(item.Name);
                    Hide();
                }
                catch (Exception ex) { System.Windows.MessageBox.Show($"Could not copy to clipboard: {ex.Message}"); }
                return;
            }
        
            if (item.Type == ResultType.Url || (item.Type == ResultType.RecentWebSearch && IsLikelyUrl(item.FullPath)))
            {
                try
                {
                    string url = item.FullPath;
                    if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "https://" + url;
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    item.Type = ResultType.RecentWebSearch;
                    item.Name = item.FullPath;
                    _viewModel.AddToRecentSearches(item);
                    Hide();
                }
                catch (Exception ex) { System.Windows.MessageBox.Show($"Could not open URL: {ex.Message}"); }
                return;
            }
        
            if (item.Type == ResultType.WebSearch || item.Type == ResultType.RecentWebSearch)
            {
                try
                {
                    Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={Uri.EscapeDataString(item.FullPath)}") { UseShellExecute = true });
                    item.Type = ResultType.RecentWebSearch;
                    _viewModel.AddToRecentSearches(item);
                    Hide();
                }
                catch (Exception ex) { System.Windows.MessageBox.Show($"Could not open web browser: {ex.Message}"); }
                return;
            }
        
            if (item.FullPath == "EXIT_COMMAND")
            {
                System.Windows.Application.Current.Shutdown();
                return;
            }
            if (item.FullPath == "SETTINGS_COMMAND")
            {
                OpenSettingsWindow();
                return;
            }
        
            try
            {
                Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true });
                UsageAnalytics.IncrementLaunchCount(item.FullPath);
                UsageAnalytics.SaveAnalytics();
                item.LaunchCount++;
                _viewModel.AddToRecentFiles(item);
                Hide();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not open item: {ex.Message}");
            }
            finally
            {
                SearchBox.Clear();
            }
        }

        private bool IsLikelyUrl(string text)
        {
            if (text.Contains(" ")) return false;
            return text.Contains(".") && (text.EndsWith(".com") || text.EndsWith(".net") || text.EndsWith(".org") || text.EndsWith(".io") || text.EndsWith(".gov") || text.EndsWith(".edu"));
        }

        private void RunAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SearchResult item)
            {
                if (item.IsSpecialCommand) return;
                try
                {
                    var proc = new ProcessStartInfo(item.FullPath) { UseShellExecute = true, Verb = "runas" };
                    Process.Start(proc);
                    UsageAnalytics.IncrementLaunchCount(item.FullPath);
                    UsageAnalytics.SaveAnalytics();
                    item.LaunchCount++;
                    _viewModel.AddToRecentFiles(item);
                    Hide();
                    SearchBox.Clear();
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.Message.Contains("cancelled")) { }
                catch (Exception ex) { System.Windows.MessageBox.Show($"Could not run as administrator: {ex.Message}"); }
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
             if ((sender as FrameworkElement)?.DataContext is SearchResult item)
            {
                if (item.IsSpecialCommand) return;
                if (File.Exists(item.FullPath) || Directory.Exists(item.FullPath))
                {
                    try { Process.Start("explorer.exe", $"/select,\"{item.FullPath}\""); }
                    catch (Exception ex) { System.Windows.MessageBox.Show($"Could not open file location: {ex.Message}"); }
                }
            }
        }

        private void DeleteRecentItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SearchResult itemToRemove)
            {
                if (itemToRemove.IsSpecialCommand) return;
                if (itemToRemove.GroupName == "Recent Searches")
                {
                    _viewModel.RecentSearches.RemoveAll(s => s.FullPath == itemToRemove.FullPath);
                    _viewModel.SaveRecentSearches();
                }
                else
                {
                    _viewModel.RecentFiles.RemoveAll(r => r.FullPath == itemToRemove.FullPath);
                    _viewModel.SaveRecentFiles();
                }
                _viewModel.UpdateDefaultView();
            }
        }
        
        private void ResultsBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsBox.SelectedItem is SearchResult selectedResult)
            {
                LaunchItem(selectedResult);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) { WindowAccent.DisableBlur(this); DragMove(); } }
        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { WindowAccent.EnableBlur(this); this.Background = System.Windows.Media.Brushes.Transparent; }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) { _hotkeyHelper?.Unregister(); _notifyIcon?.Dispose(); }
        private void SetPlaceholder() { _isProgrammaticallyChangingText = true; SearchBox.Text = PlaceholderText; SearchBox.Foreground = System.Windows.Media.Brushes.Gray; _isProgrammaticallyChangingText = false; }
        private void RemovePlaceholder() { _isProgrammaticallyChangingText = true; if (SearchBox.Text == PlaceholderText) { SearchBox.Text = ""; } SearchBox.Foreground = System.Windows.Media.Brushes.White; _isProgrammaticallyChangingText = false; }
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e) { if (SearchBox.Text == PlaceholderText) { RemovePlaceholder(); } }
        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) { if (string.IsNullOrWhiteSpace(SearchBox.Text)) { SetPlaceholder(); } }
        private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ResultsBox.SelectedItem is SearchResult selectedResult) { LaunchItem(selectedResult); e.Handled = true; }
            else if (ResultsBox.Items.Count > 0)
            {
                int newIndex = ResultsBox.SelectedIndex;
                bool keyHandled = false;
                if (e.Key == Key.Down) { newIndex = (ResultsBox.SelectedIndex + 1) % ResultsBox.Items.Count; keyHandled = true; }
                else if (e.Key == Key.Up) { newIndex = (ResultsBox.SelectedIndex - 1 + ResultsBox.Items.Count) % ResultsBox.Items.Count; keyHandled = true; }
                if (keyHandled) { ResultsBox.SelectedIndex = newIndex; ResultsBox.ScrollIntoView(ResultsBox.SelectedItem); e.Handled = true; }
            }
        }

        public void FocusSearchBox() { SearchBox.Focus(); }
        private void Window_Deactivated(object sender, EventArgs e) { if (this.IsVisible) { this.Hide(); Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) // CHANGED: Restored event handler
        {
            if (_isProgrammaticallyChangingText) return;
            if (DataContext is LauncherViewModel vm)
            {
                vm.SearchText = SearchBox.Text;
            }
        }
    }
}