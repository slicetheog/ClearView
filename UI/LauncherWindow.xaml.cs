using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SpotlightClean.Data;
using SpotlightClean.Logic;
using SpotlightClean.Utils;

namespace SpotlightClean.UI
{
    public partial class LauncherWindow : Window
    {
        private List<SearchResult> fileSystemIndex = new List<SearchResult>();
        private List<SearchResult> recentFiles = new List<SearchResult>();
        private List<SearchResult> recentSearches = new List<SearchResult>();
        private const int MaxRecentFiles = 5;
        private const int MaxRecentSearches = 5;
        private readonly string recentFilesPath;
        private readonly string recentSearchesPath;
        private readonly string settingsPath;
        private readonly string indexingSettingsPath;
        private readonly string exclusionSettingsPath;
        private readonly string indexFilePath;
        private readonly string generalSettingsPath;

        private List<string> searchScope = new List<string>();
        private IndexingSettings indexingSettings = new IndexingSettings();
        private ExclusionSettings exclusionSettings = new ExclusionSettings();
        private GeneralSettings generalSettings = new GeneralSettings();
        private bool _isIndexing = false;
        
        private NotifyIcon? _notifyIcon;
        private HotkeyHelper? _hotkeyHelper;
        
        private DispatcherTimer _searchTimer;
        private Progress<long> _progress;

        private const string PlaceholderText = "Search files, apps, and more...";
        private bool _isProgrammaticallyChangingText = false;

        public LauncherWindow()
        {
            InitializeComponent();
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpotlightClean");
            Directory.CreateDirectory(appDataPath);
            recentFilesPath = Path.Combine(appDataPath, "recent.json");
            recentSearchesPath = Path.Combine(appDataPath, "recentSearches.json");
            settingsPath = Path.Combine(appDataPath, "drives.json");
            indexingSettingsPath = Path.Combine(appDataPath, "indexing.json");
            exclusionSettingsPath = Path.Combine(appDataPath, "exclusions.json");
            generalSettingsPath = Path.Combine(appDataPath, "general.json");
            indexFilePath = Path.Combine(appDataPath, "fileSystemIndex.json");

            _searchTimer = new DispatcherTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(200);
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); UpdateDisplayedResults(); };

            _progress = new Progress<long>(processedCount =>
            {
                IndexingStatusText.Text = $"{processedCount:N0} items indexed";
            });

            SetPlaceholder();
        }
        
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadDriveSettings();
                LoadIndexingSettings();
                LoadExclusionSettings();
                LoadGeneralSettings();
                LoadRecentFiles();
                LoadRecentSearches();
                await CheckAndBuildIndexAsync();
                WindowAccent.EnableBlur(this);
                SetupSystemTrayIcon();
                SetupHotkey();
            
                UpdateDefaultView();
                FocusSearchBox();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"A critical error occurred on startup: {ex.Message}\n\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void LoadDriveSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    var savedDrives = JsonSerializer.Deserialize<List<string>>(json);
                    if (savedDrives != null && savedDrives.Any())
                    {
                        searchScope = savedDrives;
                    }
                }
            }
            catch { }

            if (!searchScope.Any())
            {
                try
                {
                    searchScope.Add(DriveInfo.GetDrives().First(d => d.IsReady).Name);
                }
                catch { }
            }
        }

        private void LoadIndexingSettings()
        {
            try
            {
                if (File.Exists(indexingSettingsPath))
                {
                    string json = File.ReadAllText(indexingSettingsPath);
                    indexingSettings = JsonSerializer.Deserialize<IndexingSettings>(json) ?? new IndexingSettings();
                }
            }
            catch 
            {
                indexingSettings = new IndexingSettings();
            }
        }

        private void LoadExclusionSettings()
        {
            try
            {
                if (File.Exists(exclusionSettingsPath))
                {
                    string json = File.ReadAllText(exclusionSettingsPath);
                    exclusionSettings = JsonSerializer.Deserialize<ExclusionSettings>(json) ?? new ExclusionSettings();
                }
            }
            catch 
            {
                exclusionSettings = new ExclusionSettings();
            }
        }

        private void LoadGeneralSettings()
        {
            try
            {
                if (File.Exists(generalSettingsPath))
                {
                    string json = File.ReadAllText(generalSettingsPath);
                    generalSettings = JsonSerializer.Deserialize<GeneralSettings>(json) ?? new GeneralSettings();
                }
            }
            catch 
            {
                generalSettings = new GeneralSettings();
            }
        }

        private void SaveIndexingSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(indexingSettings);
                File.WriteAllText(indexingSettingsPath, json);
            }
            catch {}
        }

        private void SaveExclusionSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(exclusionSettings);
                File.WriteAllText(exclusionSettingsPath, json);
            }
            catch { }
        }

        private void SaveGeneralSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(generalSettings);
                File.WriteAllText(generalSettingsPath, json);
            }
            catch { }
        }
        
        private void SetupSystemTrayIcon()
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
                            Text = "SpotlightClean"
                        };
                        _notifyIcon.Click += (s, args) => ShowLauncher();
                    }
                }
            }
            catch(Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not create tray icon: {ex.Message}");
            }
        }
        
        private void SetupHotkey()
        {
            _hotkeyHelper?.Unregister();
            _hotkeyHelper = new HotkeyHelper(this, generalSettings);
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
                UpdateDefaultView(); 
                FocusSearchBox();
            }
        }
        
        private async void OpenSettingsWindow()
        {
            var settingsWindow = new SettingsWindow(new List<string>(searchScope), indexingSettings, exclusionSettings, generalSettings);
            if (settingsWindow.ShowDialog() == true)
            {
                searchScope = settingsWindow.SelectedDrives;
                indexingSettings = settingsWindow.IndexingSettings;
                exclusionSettings = settingsWindow.ExclusionSettings;
                generalSettings = settingsWindow.GeneralSettings;
                
                SaveDriveSettings();
                SaveIndexingSettings();
                SaveExclusionSettings();
                SaveGeneralSettings();

                if (settingsWindow.HotkeyChanged)
                {
                    SetupHotkey();
                    var messageWindow = new MessageWindow("Hotkey Changed", "Please restart SpotlightClean for the new hotkey to take effect.");
                    messageWindow.Owner = this;
                    messageWindow.ShowDialog();
                }

                if (settingsWindow.ShouldRebuildIndex)
                {
                    await BuildFileSystemIndexAsync(true); 
                }
            }
        }

        private void SaveDriveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(searchScope);
                File.WriteAllText(settingsPath, json);
            }
            catch { }
        }

        private async Task CheckAndBuildIndexAsync()
        {
            bool shouldIndex = false;
            switch (indexingSettings.Schedule)
            {
                case IndexingSchedule.OnStartup:
                    shouldIndex = true;
                    break;
                case IndexingSchedule.Interval:
                    TimeSpan interval = GetIntervalTimeSpan();
                    if (DateTime.UtcNow - indexingSettings.LastIndexedUtc > interval)
                    {
                        shouldIndex = true;
                    }
                    break;
                case IndexingSchedule.Manual:
                default:
                    shouldIndex = false; 
                    break;
            }

            if (!File.Exists(indexFilePath))
            {
                shouldIndex = true;
            }

            if (shouldIndex)
            {
                await BuildFileSystemIndexAsync(true);
            }
            else
            {
                await BuildFileSystemIndexAsync(false); 
            }
        }

        private TimeSpan GetIntervalTimeSpan()
        {
            return indexingSettings.IntervalUnit switch
            {
                "Days" => TimeSpan.FromDays(indexingSettings.IntervalValue),
                "Weeks" => TimeSpan.FromDays(indexingSettings.IntervalValue * 7),
                _ => TimeSpan.FromHours(indexingSettings.IntervalValue),
            };
        }

        private async Task BuildFileSystemIndexAsync(bool forceRebuild)
        {
            if (_isIndexing) return;
            
            _isIndexing = true;
            SearchBox.IsEnabled = false;
            ResultsBox.Visibility = Visibility.Collapsed;
            
            if (forceRebuild)
            {
                IndexingTitleText.Visibility = Visibility.Visible;
                LoadingTitleText.Visibility = Visibility.Collapsed;
                IndexingStatusText.Visibility = Visibility.Visible;
                IndexingStatusText.Text = "0 items indexed";
            }
            else
            {
                IndexingTitleText.Visibility = Visibility.Collapsed;
                LoadingTitleText.Visibility = Visibility.Visible;
                IndexingStatusText.Visibility = Visibility.Collapsed;
            }
            
            IndexingPanel.Visibility = Visibility.Visible;
            
            await Task.Yield(); 

            try
            {
                if (forceRebuild)
                {
                    fileSystemIndex = new List<SearchResult>();
                    ((CollectionViewSource)this.Resources["GroupedResults"]).Source = null;
                    
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    using (var indexer = new Indexer())
                    {
                         fileSystemIndex = await indexer.BuildIndexAsync(searchScope, exclusionSettings, _progress);
                    }
                    indexingSettings.LastIndexedUtc = DateTime.UtcNow;
                    SaveIndexingSettings();
                }
                else
                {
                    using (var indexer = new Indexer())
                    {
                        fileSystemIndex = await indexer.LoadIndexFromCacheAsync();
                    }
                }
                
                if (fileSystemIndex != null)
                {
                    foreach (var item in fileSystemIndex)
                    {
                        item.LaunchCount = UsageAnalytics.GetLaunchCount(item.FullPath);
                    }
                }
            }
            finally
            {
                IndexingPanel.Visibility = Visibility.Collapsed;
                SearchBox.IsEnabled = true;
                SearchBox.Focus();
                _isIndexing = false;
                UpdateDisplayedResults();
                
                if (_notifyIcon != null && forceRebuild)
                {
                    _notifyIcon.ShowBalloonTip(2000, "Indexing Complete", "Your file index has been successfully updated.", ToolTipIcon.Info);
                }
            }
        }

        private void LoadRecentFiles()
        {
            try
            {
                if (File.Exists(recentFilesPath))
                {
                    string json = File.ReadAllText(recentFilesPath);
                    recentFiles = JsonSerializer.Deserialize<List<SearchResult>>(json) ?? new List<SearchResult>();
                }
            }
            catch {}
        }

        private void LoadRecentSearches()
        {
            try
            {
                if (File.Exists(recentSearchesPath))
                {
                    string json = File.ReadAllText(recentSearchesPath);
                    recentSearches = JsonSerializer.Deserialize<List<SearchResult>>(json) ?? new List<SearchResult>();
                }
            }
            catch {}
        }

        private void SaveRecentFiles()
        {
            try
            {
                string? directory = Path.GetDirectoryName(recentFilesPath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                    var recentFilesToSave = recentFiles.Select(r => new SearchResult { Name = r.Name, FullPath = r.FullPath, Type = r.Type, LaunchCount = r.LaunchCount }).ToList();
                    string json = JsonSerializer.Serialize(recentFilesToSave);
                    File.WriteAllText(recentFilesPath, json);
                }
            }
            catch {}
        }

        private void SaveRecentSearches()
        {
            try
            {
                string? directory = Path.GetDirectoryName(recentSearchesPath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                    var recentSearchesToSave = recentSearches.Select(r => new SearchResult { Name = r.Name, FullPath = r.FullPath, Type = r.Type }).ToList();
                    string json = JsonSerializer.Serialize(recentSearchesToSave);
                    File.WriteAllText(recentSearchesPath, json);
                }
            }
            catch {}
        }

        private void AddToRecentFiles(SearchResult selectedResult)
        {
            if (selectedResult.IsSpecialCommand) return;
            recentFiles.RemoveAll(r => r.FullPath == selectedResult.FullPath);
            recentFiles.Insert(0, selectedResult);
            if (recentFiles.Count > MaxRecentFiles)
            {
                recentFiles = recentFiles.GetRange(0, MaxRecentFiles);
            }
            SaveRecentFiles();
        }

        private void AddToRecentSearches(SearchResult selectedResult)
        {
            recentSearches.RemoveAll(s => s.FullPath == selectedResult.FullPath);
            recentSearches.Insert(0, selectedResult);
            if (recentSearches.Count > MaxRecentSearches)
            {
                recentSearches = recentSearches.GetRange(0, MaxRecentSearches);
            }
            SaveRecentSearches();
        }

        public void UpdateDefaultView()
        {
            var displayList = new List<SearchResult>();

            if (recentFiles.Any())
            {
                foreach (var recentFile in recentFiles)
                {
                    recentFile.GroupName = "Recently Opened";
                    displayList.Add(recentFile);
                }
            }

            if (recentSearches.Any())
            {
                foreach (var recentSearch in recentSearches)
                {
                    recentSearch.GroupName = "Recent Searches";
                    displayList.Add(recentSearch);
                }
            }

            displayList.Add(new SearchResult { Name = "Settings", FullPath = "SETTINGS_COMMAND", Type = ResultType.Application, IsSpecialCommand = true, GroupName = "Spotlight Commands" });
            displayList.Add(new SearchResult { Name = "Exit", FullPath = "EXIT_COMMAND", Type = ResultType.Application, IsSpecialCommand = true, GroupName = "Spotlight Commands" });
            
            var cvs = (CollectionViewSource)this.Resources["GroupedResults"];
            cvs.Source = displayList;
            
            ResultsBox.Visibility = Visibility.Visible;
            if (ResultsBox.Items.Count > 0)
            {
                ResultsBox.SelectedIndex = 0;
            }
        }
        
        private int CalculateScore(SearchResult item, string searchText)
        {
            var itemName = item.Name.ToLower();
            if (!itemName.Contains(searchText))
            {
                return 0;
            }

            int score = 0;
            score += item.LaunchCount * 5000;

            if (itemName.Equals(searchText, StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;
            }
            else if (itemName.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
            {
                score += 500;
            }
            else
            {
                score += 100;
            }

            switch (item.Type)
            {
                case ResultType.Application:
                    score += 200;
                    break;
                case ResultType.Folder:
                    score += 50;
                    break;
            }
            
            score -= item.FullPath.Count(c => c == Path.DirectorySeparatorChar);

            return score;
        }

        private bool IsLikelyUrl(string text)
        {
            if (text.Contains(" ")) return false;
            return text.Contains(".") && (text.EndsWith(".com") || text.EndsWith(".net") || text.EndsWith(".org") || text.EndsWith(".io") || text.EndsWith(".gov") || text.EndsWith(".edu"));
        }

        private async void UpdateDisplayedResults()
        {
            string query = SearchBox.Text;
            string lowerQuery = query.ToLower();

            if (string.IsNullOrWhiteSpace(lowerQuery))
            {
                UpdateDefaultView();
                return;
            }

            if (IsLikelyUrl(query))
            {
                var results = new List<SearchResult>
                {
                    new SearchResult { Name = $"Open {query}", FullPath = query, Type = ResultType.Url, GroupName = "Go to Address" }
                };
                var cvs = (CollectionViewSource)this.Resources["GroupedResults"];
                cvs.Source = results;
                ResultsBox.Visibility = Visibility.Visible;
                ResultsBox.SelectedIndex = 0;
                return;
            }
            
            string? calculatorResult = Calculator.Evaluate(query);
            if (calculatorResult != null)
            {
                var results = new List<SearchResult>
                {
                    new SearchResult { Name = calculatorResult, Type = ResultType.Calculator, GroupName = "Calculator" }
                };
                var cvs = (CollectionViewSource)this.Resources["GroupedResults"];
                cvs.Source = results;
                ResultsBox.Visibility = Visibility.Visible;
                ResultsBox.SelectedIndex = 0;
                return;
            }

            string? converterResult = await UnitConverter.Convert(lowerQuery);
            if (converterResult != null)
            {
                var results = new List<SearchResult>
                {
                    new SearchResult { Name = converterResult, Type = ResultType.Calculator, GroupName = "Converter" }
                };
                var cvs = (CollectionViewSource)this.Resources["GroupedResults"];
                cvs.Source = results;
                ResultsBox.Visibility = Visibility.Visible;
                ResultsBox.SelectedIndex = 0;
                return;
            }
            
            await Task.Run(async () =>
            {
                var results = fileSystemIndex
                    ?.Select(item => new { Item = item, Score = CalculateScore(item, lowerQuery) })
                    .Where(scoredItem => scoredItem.Score > 0)
                    .OrderByDescending(scoredItem => scoredItem.Score)
                    .Select(scoredItem => scoredItem.Item)
                    .Take(100)
                    .ToList();
                
                await Dispatcher.InvokeAsync(() =>
                {
                    if (results != null && results.Any())
                    {
                        foreach (var result in results)
                        {
                            result.GroupName = GetGroupName(result.Type);
                        }

                        var cvs = (CollectionViewSource)this.Resources["GroupedResults"];
                        cvs.Source = results;

                        ResultsBox.Visibility = Visibility.Visible;
                        if (ResultsBox.Items.Count > 0)
                        {
                           ResultsBox.SelectedIndex = 0;
                        }
                    }
                    else
                    {
                        var webSearchResult = new List<SearchResult>
                        {
                            new SearchResult { Name = $"Search for \"{query}\"", FullPath = query, Type = ResultType.WebSearch, GroupName = "Web Search" }
                        };
                        var cvs = (CollectionViewSource)this.Resources["GroupedResults"];
                        cvs.Source = webSearchResult;
                        ResultsBox.Visibility = Visibility.Visible;
                        ResultsBox.SelectedIndex = 0;
                    }
                });
            });
        }

        private string GetGroupName(ResultType type)
        {
            return type switch
            {
                ResultType.Application => "Apps",
                ResultType.Folder => "Folders",
                ResultType.File => "Files",
                _ => "Other"
            };
        }


        private void LaunchItem(SearchResult item)
        {
            if (item == null) return;

            if (item.Type == ResultType.Calculator)
            {
                try
                {
                    System.Windows.Clipboard.SetText(item.Name);
                    Hide();
                    SearchBox.Clear();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not copy to clipboard: {ex.Message}");
                }
                return;
            }

            // // CHANGED: If a recent search item is a URL, launch it as a URL.
            if (item.Type == ResultType.Url || (item.Type == ResultType.RecentWebSearch && IsLikelyUrl(item.FullPath)))
            {
                try
                {
                    string url = item.FullPath;
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    {
                        url = "https://" + url;
                    }
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    
                    item.Type = ResultType.RecentWebSearch;
                    item.Name = item.FullPath;
                    AddToRecentSearches(item);

                    Hide();
                    SearchBox.Clear();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not open URL: {ex.Message}");
                }
                return;
            }

            if (item.Type == ResultType.WebSearch || item.Type == ResultType.RecentWebSearch)
            {
                try
                {
                    Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={Uri.EscapeDataString(item.FullPath)}") { UseShellExecute = true });
                    
                    item.Type = ResultType.RecentWebSearch;
                    AddToRecentSearches(item);

                    Hide();
                    SearchBox.Clear();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not open web browser: {ex.Message}");
                }
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

                AddToRecentFiles(item);
                Hide();
                SearchBox.Clear();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not open item: {ex.Message}");
            }
        }
        
        private void RunAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SearchResult item)
            {
                if (item.IsSpecialCommand) return;

                try
                {
                    var proc = new ProcessStartInfo(item.FullPath)
                    {
                        UseShellExecute = true,
                        Verb = "runas" 
                    };
                    Process.Start(proc);
                    
                    UsageAnalytics.IncrementLaunchCount(item.FullPath);
                    UsageAnalytics.SaveAnalytics();
                    item.LaunchCount++;
                    AddToRecentFiles(item);
                    
                    Hide();
                    SearchBox.Clear();
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.Message.Contains("cancelled"))
                {
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Could not run as administrator: {ex.Message}");
                }
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SearchResult item)
            {
                if (item.IsSpecialCommand) return;
                
                if (File.Exists(item.FullPath) || Directory.Exists(item.FullPath))
                {
                    try
                    {
                        Process.Start("explorer.exe", $"/select,\"{item.FullPath}\"");
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Could not open file location: {ex.Message}");
                    }
                }
            }
        }

        private void DeleteRecentItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SearchResult itemToRemove)
            {
                if (itemToRemove.IsSpecialCommand) return;
                
                if (itemToRemove.Type == ResultType.RecentWebSearch)
                {
                    recentSearches.RemoveAll(s => s.FullPath == itemToRemove.FullPath);
                    SaveRecentSearches();
                }
                else
                {
                    recentFiles.RemoveAll(r => r.FullPath == itemToRemove.FullPath);
                    SaveRecentFiles();
                }
                
                UpdateDefaultView();
            }
        }

        private void ResultsBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsBox.SelectedItem is SearchResult selectedResult)
            {
                LaunchItem(selectedResult);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                WindowAccent.DisableBlur(this);
                DragMove();
            }
        }
        
        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            WindowAccent.EnableBlur(this);
            this.Background = System.Windows.Media.Brushes.Transparent;
        }
        
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _hotkeyHelper?.Unregister();
            _notifyIcon?.Dispose();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isProgrammaticallyChangingText) return;
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void SetPlaceholder()
        {
            _isProgrammaticallyChangingText = true;
            SearchBox.Text = PlaceholderText;
            SearchBox.Foreground = System.Windows.Media.Brushes.Gray;
            _isProgrammaticallyChangingText = false;
        }

        private void RemovePlaceholder()
        {
            _isProgrammaticallyChangingText = true;
            SearchBox.Text = "";
            SearchBox.Foreground = System.Windows.Media.Brushes.White;
            _isProgrammaticallyChangingText = false;
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == PlaceholderText)
            {
                RemovePlaceholder();
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SetPlaceholder();
            }
        }

        private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ResultsBox.SelectedItem is SearchResult selectedResult)
            {
                LaunchItem(selectedResult);
                e.Handled = true; 
            }
            else if (ResultsBox.Items.Count > 0)
            {
                int newIndex = ResultsBox.SelectedIndex;
                bool keyHandled = false;

                if (e.Key == Key.Down)
                {
                    newIndex = (ResultsBox.SelectedIndex + 1) % ResultsBox.Items.Count;
                    keyHandled = true;
                }
                else if (e.Key == Key.Up)
                {
                    newIndex = (ResultsBox.SelectedIndex - 1 + ResultsBox.Items.Count) % ResultsBox.Items.Count;
                    keyHandled = true;
                }

                if (keyHandled)
                {
                    ResultsBox.SelectedIndex = newIndex;
                    ResultsBox.ScrollIntoView(ResultsBox.SelectedItem);
                    e.Handled = true;
                }
            }
        }

        public void FocusSearchBox() { SearchBox.Focus(); }
        private void Window_Deactivated(object sender, EventArgs e) 
        { 
            if (this.IsVisible)
            {
                this.Hide();
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
            }
        }
    }
}