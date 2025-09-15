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
        private readonly string _generalSettingsPath;
        private readonly string _indexFilePath;

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
            set
            {
                _isIndexingInProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowIndexingTitle));
                OnPropertyChanged(nameof(ShowLoadingTitle));
            }
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
            set
            {
                _isLoadingFromCache = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowIndexingTitle));
                OnPropertyChanged(nameof(ShowLoadingTitle));
            }
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
            _searchTimer.Tick += async (s, e) =>
            {
                _searchTimer.Stop();
                await UpdateDisplayedResults();
            };

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

        private void LoadDriveSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var savedDrives = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_settingsPath));
                    if (savedDrives != null && savedDrives.Any())
                    {
                        _searchScope = savedDrives;
                    }
                }

                if (!_searchScope.Any())
                {
                    _searchScope.Add(DriveInfo.GetDrives().First(d => d.IsReady).Name);
                }
            }
            catch { }
        }

        private void LoadIndexingSettings()
        {
            try
            {
                if (File.Exists(_indexingSettingsPath))
                {
                    _indexingSettings = JsonSerializer.Deserialize<IndexingSettings>(File.ReadAllText(_indexingSettingsPath)) ?? new IndexingSettings();
                }
            }
            catch
            {
                _indexingSettings = new IndexingSettings();
            }
        }

        private void LoadExclusionSettings()
        {
            try
            {
                if (File.Exists(_exclusionSettingsPath))
                {
                    _exclusionSettings = JsonSerializer.Deserialize<ExclusionSettings>(File.ReadAllText(_exclusionSettingsPath)) ?? new ExclusionSettings();
                }
            }
            catch
            {
                _exclusionSettings = new ExclusionSettings();
            }
        }

        private void LoadGeneralSettings()
        {
            try
            {
                if (File.Exists(_generalSettingsPath))
                {
                    _generalSettings = JsonSerializer.Deserialize<GeneralSettings>(File.ReadAllText(_generalSettingsPath)) ?? new GeneralSettings();
                }
            }
            catch
            {
                _generalSettings = new GeneralSettings();
            }
        }

        private void LoadRecentFiles()
        {
            try
            {
                if (File.Exists(_recentFilesPath))
                {
                    _recentFiles = JsonSerializer.Deserialize<List<SearchResult>>(File.ReadAllText(_recentFilesPath)) ?? new List<SearchResult>();
                }
            }
            catch { }
        }

        private void LoadRecentSearches()
        {
            try
            {
                if (File.Exists(_recentSearchesPath))
                {
                    _recentSearches = JsonSerializer.Deserialize<List<SearchResult>>(File.ReadAllText(_recentSearchesPath)) ?? new List<SearchResult>();
                }
            }
            catch { }
        }

        public void UpdateDefaultView()
        {
            var displayList = new List<SearchResult>();

            if (_recentFiles.Any())
            {
                displayList.AddRange(_recentFiles.Select(r =>
                {
                    r.GroupName = "Recently Opened";
                    return r;
                }));
            }

            if (_recentSearches.Any())
            {
                displayList.AddRange(_recentSearches.Select(s =>
                {
                    s.GroupName = "Recent Searches";
                    return s;
                }));
            }

            displayList.Add(new SearchResult
            {
                Name = "Settings",
                FullPath = "SETTINGS_COMMAND",
                Type = ResultType.Application,
                IsSpecialCommand = true,
                GroupName = "ClearView Commands"
            });

            displayList.Add(new SearchResult
            {
                Name = "Exit",
                FullPath = "EXIT_COMMAND",
                Type = ResultType.Application,
                IsSpecialCommand = true,
                GroupName = "ClearView Commands"
            });

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

            var results = _fileSystemIndex
                .Where(r => r.Name != null && r.Name.ToLower().Contains(lowerQuery))
                .Select(r =>
                {
                    r.GroupName = r.Type == ResultType.Folder ? "Folders" :
                                  r.Type == ResultType.Application ? "Applications" : "Files";
                    return r;
                })
                .ToList();

            _groupedResults.Source = results;
            OnPropertyChanged(nameof(GroupedResults));
        }

        public void SaveIndexingSettings()
        {
            try { File.WriteAllText(_indexingSettingsPath, JsonSerializer.Serialize(_indexingSettings)); }
            catch { }
        }

        public void SaveExclusionSettings()
        {
            try { File.WriteAllText(_exclusionSettingsPath, JsonSerializer.Serialize(_exclusionSettings)); }
            catch { }
        }

        public void SaveGeneralSettings()
        {
            try { File.WriteAllText(_generalSettingsPath, JsonSerializer.Serialize(_generalSettings)); }
            catch { }
        }

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

                    var progress = new Progress<long>(c =>
                    {
                        IndexingStatusText = $"{c:N0} items indexed";
                    });

                    _fileSystemIndex = new List<SearchResult>();
                    _groupedResults.Source = null;
                    GC.Collect();

                    using (var indexer = new Indexer())
                    {
                        _fileSystemIndex = await indexer.BuildIndexAsync(_searchScope, _exclusionSettings, _generalSettings.SearchMode, progress);
                    }

                    _indexingSettings.LastIndexedUtc = DateTime.UtcNow;
                    SaveIndexingSettings();
                }
                else
                {
                    IsLoadingFromCache = true;
                    using (var indexer = new Indexer())
                    {
                        _fileSystemIndex = await indexer.LoadIndexFromCacheAsync();
                    }
                }

                if (_fileSystemIndex != null)
                {
                    foreach (var item in _fileSystemIndex)
                    {
                        item.LaunchCount = UsageAnalytics.GetLaunchCount(item.FullPath);
                    }
                }
            }
            finally
            {
                _isIndexing = false;
                IsIndexingInProgress = false;
                UpdateDefaultView();
            }
        }

        // -------------------------------
        // Recent searches and files (fixed signatures)
        // -------------------------------

        public void AddToRecentSearches(SearchResult search)
        {
            if (search == null || string.IsNullOrWhiteSpace(search.Name)) return;

            search.GroupName = "Recent Searches";

            _recentSearches.RemoveAll(r => r.FullPath == search.FullPath);
            _recentSearches.Insert(0, search);
            if (_recentSearches.Count > MaxRecentSearches)
                _recentSearches.RemoveAt(_recentSearches.Count - 1);

            SaveRecentSearches();
        }

        public void SaveRecentSearches()
        {
            try { File.WriteAllText(_recentSearchesPath, JsonSerializer.Serialize(_recentSearches)); }
            catch { }
        }

        public void AddToRecentFiles(SearchResult file)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.FullPath)) return;

            file.GroupName = "Recently Opened";

            _recentFiles.RemoveAll(r => r.FullPath == file.FullPath);
            _recentFiles.Insert(0, file);
            if (_recentFiles.Count > MaxRecentFiles)
                _recentFiles.RemoveAt(_recentFiles.Count - 1);

            SaveRecentFiles();
        }

        public void SaveRecentFiles()
        {
            try { File.WriteAllText(_recentFilesPath, JsonSerializer.Serialize(_recentFiles)); }
            catch { }
        }
    }
}
