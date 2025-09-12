using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows;
using ClearView.Data;
using ClearView.Logic;
using ClearView.Utils;

namespace ClearView.ViewModel
{
    public class LauncherViewModel : BaseViewModel
    {
        private List<SearchResult> _fileSystemIndex = new List<SearchResult>();
        private List<SearchResult> _recentFiles = new List<SearchResult>();
        private List<SearchResult> _recentSearches = new List<SearchResult>();
        private const int MaxRecentFiles = 5;
        private const int MaxRecentSearches = 5;
        private readonly string _recentFilesPath;
        private readonly string _recentSearchesPath;
        private readonly string _settingsPath;
        private readonly string _indexingSettingsPath;
        private readonly string _exclusionSettingsPath;
        private readonly string _indexFilePath;
        private readonly string _generalSettingsPath;

        private List<string> _searchScope = new List<string>();
        private IndexingSettings _indexingSettings = new IndexingSettings();
        private ExclusionSettings _exclusionSettings = new ExclusionSettings();
        private GeneralSettings _generalSettings = new GeneralSettings();
        private bool _isIndexing = false;
        private DispatcherTimer _searchTimer;

        private bool _isIndexingInProgress;
        public bool IsIndexingInProgress
        {
            get => _isIndexingInProgress;
            set { _isIndexingInProgress = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowIndexingTitle)); OnPropertyChanged(nameof(ShowLoadingTitle)); }
        }

        private string _indexingStatusText = "0 items indexed";
        public string IndexingStatusText
        {
            get => _indexingStatusText;
            set { _indexingStatusText = value; OnPropertyChanged(); }
        }

        private bool _isLoadingFromCache;
        public bool IsLoadingFromCache
        {
            get => _isLoadingFromCache;
            set { _isLoadingFromCache = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowIndexingTitle)); OnPropertyChanged(nameof(ShowLoadingTitle)); }
        }
        
        public bool ShowIndexingTitle => IsIndexingInProgress && !IsLoadingFromCache;
        public bool ShowLoadingTitle => IsIndexingInProgress && IsLoadingFromCache;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    _searchTimer.Stop();
                    _searchTimer.Start();
                }
            }
        }

        private CollectionViewSource _groupedResults = new CollectionViewSource();
        public ICollectionView GroupedResults => _groupedResults.View;
        public List<string> SearchScope { get => _searchScope; set => _searchScope = value; }
        public IndexingSettings IndexingSettings { get => _indexingSettings; set => _indexingSettings = value; }
        public ExclusionSettings ExclusionSettings { get => _exclusionSettings; set => _exclusionSettings = value; }
        public GeneralSettings GeneralSettings { get => _generalSettings; set => _generalSettings = value; }
        public List<SearchResult> RecentFiles => _recentFiles;
        public List<SearchResult> RecentSearches => _recentSearches;

        public LauncherViewModel()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearView");
            Directory.CreateDirectory(appDataPath);
            _recentFilesPath = Path.Combine(appDataPath, "recent.json");
            _recentSearchesPath = Path.Combine(appDataPath, "recentSearches.json");
            _settingsPath = Path.Combine(appDataPath, "drives.json");
            _indexingSettingsPath = Path.Combine(appDataPath, "indexing.json");
            _exclusionSettingsPath = Path.Combine(appDataPath, "exclusions.json");
            _generalSettingsPath = Path.Combine(appDataPath, "general.json");
            _indexFilePath = Path.Combine(appDataPath, "fileSystemIndex.json");

            _groupedResults.GroupDescriptions.Add(new PropertyGroupDescription("GroupName"));
            
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _searchTimer.Tick += async (s, e) => { _searchTimer.Stop(); await UpdateDisplayedResults(); };

            Initialize();
        }

        private async void Initialize()
        {
            LoadDriveSettings();
            LoadIndexingSettings();
            LoadExclusionSettings();
            LoadGeneralSettings();
            LoadRecentFiles();
            LoadRecentSearches();
            await BuildFileSystemIndexAsync(false);
            UpdateDefaultView();
        }

        private void LoadDriveSettings() { try { if (File.Exists(_settingsPath)) { var savedDrives = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_settingsPath)); if (savedDrives != null && savedDrives.Any()) { _searchScope = savedDrives; } } if (!_searchScope.Any()) { _searchScope.Add(DriveInfo.GetDrives().First(d => d.IsReady).Name); } } catch { } }
        private void LoadIndexingSettings() { try { if (File.Exists(_indexingSettingsPath)) { _indexingSettings = JsonSerializer.Deserialize<IndexingSettings>(File.ReadAllText(_indexingSettingsPath)) ?? new IndexingSettings(); } } catch { _indexingSettings = new IndexingSettings(); } }
        private void LoadExclusionSettings() { try { if (File.Exists(_exclusionSettingsPath)) { _exclusionSettings = JsonSerializer.Deserialize<ExclusionSettings>(File.ReadAllText(_exclusionSettingsPath)) ?? new ExclusionSettings(); } } catch { _exclusionSettings = new ExclusionSettings(); } }
        private void LoadGeneralSettings() { try { if (File.Exists(_generalSettingsPath)) { _generalSettings = JsonSerializer.Deserialize<GeneralSettings>(File.ReadAllText(_generalSettingsPath)) ?? new GeneralSettings(); } } catch { _generalSettings = new GeneralSettings(); } }
        private void LoadRecentFiles() { try { if (File.Exists(_recentFilesPath)) { _recentFiles = JsonSerializer.Deserialize<List<SearchResult>>(File.ReadAllText(_recentFilesPath)) ?? new List<SearchResult>(); } } catch { } }
        private void LoadRecentSearches() { try { if (File.Exists(_recentSearchesPath)) { _recentSearches = JsonSerializer.Deserialize<List<SearchResult>>(File.ReadAllText(_recentSearchesPath)) ?? new List<SearchResult>(); } } catch { } }

        public void UpdateDefaultView()
        {
            var displayList = new List<SearchResult>();
            if (_recentFiles.Any()) { displayList.AddRange(_recentFiles.Select(r => { r.GroupName = "Recently Opened"; return r; })); }
            if (_recentSearches.Any()) { displayList.AddRange(_recentSearches.Select(s => { s.GroupName = "Recent Searches"; return s; })); }
            displayList.Add(new SearchResult { Name = "Settings", FullPath = "SETTINGS_COMMAND", Type = ResultType.Application, IsSpecialCommand = true, GroupName = "ClearView Commands" });
            displayList.Add(new SearchResult { Name = "Exit", FullPath = "EXIT_COMMAND", Type = ResultType.Application, IsSpecialCommand = true, GroupName = "ClearView Commands" });
            _groupedResults.Source = displayList;
            OnPropertyChanged(nameof(GroupedResults));
        }

        private async Task UpdateDisplayedResults()
        {
            string query = SearchText;
            string lowerQuery = query.ToLower();

            if (string.IsNullOrWhiteSpace(lowerQuery))
            {
                UpdateDefaultView();
                return;
            }

            // === Clipboard command handler ===
            if (query.StartsWith("clip:", StringComparison.OrdinalIgnoreCase))
            {
                var history = ClipboardHistoryManager.GetHistory();
                var results = new List<SearchResult>();
                // Add "Clear All" button at the top
                results.Add(new SearchResult
                {
                    Name = "Clear All Recent Clipboard History",
                    Type = ResultType.Command,
                    IsSpecialCommand = true,
                    GroupName = "Clipboard",
                    FullPath = "CLEAR_CLIPBOARD_COMMAND"
                });

                // Then add the clipboard history entries
                results.AddRange(history.Select(text => new SearchResult
                {
                    Name = (text.Length > 80 ? text.Substring(0, 80) + "..." : text),
                    ClipboardText = text,
                    Type = ResultType.Clipboard,
                    IsSpecialCommand = true,
                    GroupName = "Clipboard"
                }));

                _groupedResults.Source = results;
                OnPropertyChanged(nameof(GroupedResults));
                return;
            }

            if (IsLikelyUrl(query))
            {
                _groupedResults.Source = new List<SearchResult> { new SearchResult { Name = $"Open {query}", FullPath = query, Type = ResultType.Url, GroupName = "Go to Address" } };
                OnPropertyChanged(nameof(GroupedResults));
                return;
            }

            string? calculatorResult = Calculator.Evaluate(query);
            if (calculatorResult != null)
            {
                _groupedResults.Source = new List<SearchResult> { new SearchResult { Name = calculatorResult, Type = ResultType.Calculator, GroupName = "Calculator" } };
                OnPropertyChanged(nameof(GroupedResults));
                return;
            }

            string? converterResult = await UnitConverter.Convert(lowerQuery);
            if (converterResult != null)
            {
                _groupedResults.Source = new List<SearchResult> { new SearchResult { Name = converterResult, Type = ResultType.Calculator, GroupName = "Converter" } };
                OnPropertyChanged(nameof(GroupedResults));
                return;
            }

            await Task.Run(() =>
            {
                var results = _fileSystemIndex
                    ?.Select(item => new { Item = item, Score = CalculateScore(item, lowerQuery) })
                    .Where(scoredItem => scoredItem.Score > 0)
                    .OrderByDescending(scoredItem => scoredItem.Score)
                    .Select(scoredItem => scoredItem.Item)
                    .Take(100)
                    .ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (results != null && results.Any())
                    {
                        foreach (var result in results) { result.GroupName = GetGroupName(result.Type); }
                        _groupedResults.Source = results;
                    }
                    else
                    {
                        _groupedResults.Source = new List<SearchResult> { new SearchResult { Name = $"Search for \"{query}\"", FullPath = query, Type = ResultType.WebSearch, GroupName = "Web Search" } };
                    }
                    OnPropertyChanged(nameof(GroupedResults));
                });
            });
        }
        
        private bool IsLikelyUrl(string text) { if (text.Contains(" ")) return false; return text.Contains(".") && (text.EndsWith(".com") || text.EndsWith(".net") || text.EndsWith(".org") || text.EndsWith(".io") || text.EndsWith(".gov") || text.EndsWith(".edu")); }
        private int CalculateScore(SearchResult item, string searchText) { var itemName = item.Name.ToLower(); if (!itemName.Contains(searchText)) return 0; int score = item.LaunchCount * 5000; if (itemName.Equals(searchText, StringComparison.OrdinalIgnoreCase)) score += 1000; else if (itemName.StartsWith(searchText, StringComparison.OrdinalIgnoreCase)) score += 500; else score += 100; switch (item.Type) { case ResultType.Application: score += 200; break; case ResultType.Folder: score += 50; break; } score -= item.FullPath.Count(c => c == Path.DirectorySeparatorChar); return score; }
        
        private string GetGroupName(ResultType type) => type switch
        {
            ResultType.Application => "Apps",
            ResultType.Folder => "Folders",
            ResultType.File => "Files",
            ResultType.Clipboard => "Clipboard",
            ResultType.Calculator => "Calculator",
            ResultType.Url => "Go to Address",
            ResultType.WebSearch => "Web Search",
            ResultType.RecentWebSearch => "Recent Searches",
            ResultType.Command => "Commands",
            _ => "Other"
        };
        
        public void AddToRecentFiles(SearchResult r) { if (r.IsSpecialCommand) return; _recentFiles.RemoveAll(i => i.FullPath == r.FullPath); _recentFiles.Insert(0, r); if (_recentFiles.Count > MaxRecentFiles) _recentFiles.RemoveAt(MaxRecentFiles); SaveRecentFiles(); }
        public void AddToRecentSearches(SearchResult r) { _recentSearches.RemoveAll(i => i.FullPath == r.FullPath); _recentSearches.Insert(0, r); if (_recentSearches.Count > MaxRecentSearches) _recentSearches.RemoveAt(MaxRecentSearches); SaveRecentSearches(); }
        public void SaveRecentFiles() { try { File.WriteAllText(_recentFilesPath, JsonSerializer.Serialize(_recentFiles.Select(r => new SearchResult { Name = r.Name, FullPath = r.FullPath, Type = r.Type, LaunchCount = r.LaunchCount }).ToList())); } catch { } }
        public void SaveRecentSearches() { try { File.WriteAllText(_recentSearchesPath, JsonSerializer.Serialize(_recentSearches.Select(r => new SearchResult { Name = r.Name, FullPath = r.FullPath, Type = r.Type }).ToList())); } catch { } }
        public void SaveIndexingSettings() { try { File.WriteAllText(_indexingSettingsPath, JsonSerializer.Serialize(_indexingSettings)); } catch { } }
        public void SaveExclusionSettings() { try { File.WriteAllText(_exclusionSettingsPath, JsonSerializer.Serialize(_exclusionSettings)); } catch { } }
        public void SaveGeneralSettings() { try { File.WriteAllText(_generalSettingsPath, JsonSerializer.Serialize(_generalSettings)); } catch { } }
        
        public async Task BuildFileSystemIndexAsync(bool forceRebuild)
        {
            if (_isIndexing) return;
            _isIndexing = true;
            IsIndexingInProgress = true;
            try
            {
                if (forceRebuild || !File.Exists(_indexFilePath))
                {
                    IsLoadingFromCache = false;
                    IndexingStatusText = "0 items indexed";
                    var progress = new Progress<long>(c => { IndexingStatusText = $"{c:N0} items indexed"; });
                    _fileSystemIndex = new List<SearchResult>();
                    _groupedResults.Source = null;
                    GC.Collect();
                    using (var indexer = new Indexer()) { _fileSystemIndex = await indexer.BuildIndexAsync(_searchScope, _exclusionSettings, progress); }
                    _indexingSettings.LastIndexedUtc = DateTime.UtcNow;
                    SaveIndexingSettings();
                }
                else
                {
                    IsLoadingFromCache = true;
                    using (var indexer = new Indexer()) { _fileSystemIndex = await indexer.LoadIndexFromCacheAsync(); }
                }
                if (_fileSystemIndex != null) { foreach (var item in _fileSystemIndex) { item.LaunchCount = UsageAnalytics.GetLaunchCount(item.FullPath); } }
            }
            finally
            {
                _isIndexing = false;
                IsIndexingInProgress = false;
                UpdateDefaultView();
            }
        }
    }
}
