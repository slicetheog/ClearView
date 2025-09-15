// FILE: ViewModel/LauncherViewModel.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
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

        public List<string> SearchScope
        {
            get => _searchScope;
            set { _searchScope = value ?? new List<string>(); OnPropertyChanged(nameof(SearchScope)); }
        }

        public IndexingSettings IndexingSettings
        {
            get => _indexingSettings;
            set { _indexingSettings = value; OnPropertyChanged(); }
        }

        public ExclusionSettings ExclusionSettings
        {
            get => _exclusionSettings;
            set { _exclusionSettings = value; OnPropertyChanged(); }
        }

        public GeneralSettings GeneralSettings
        {
            get => _generalSettings;
            set
            {
                _generalSettings = value ?? new GeneralSettings();
                OnPropertyChanged();
                _ = UpdateDisplayedResults();
            }
        }

        public List<SearchResult> RecentFiles => _recentFiles;
        public List<SearchResult> RecentSearches => _recentSearches;

        private static readonly HashSet<string> CleanModeNoisyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".log", ".tmp", ".bak", ".cache", ".pkg", ".js"
        };

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

        #region Load/Save Methods

        private void LoadDriveSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var savedDrives = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_settingsPath));
                    if (savedDrives != null && savedDrives.Any())
                        _searchScope = savedDrives;
                }
                if (!_searchScope.Any())
                    _searchScope.Add(DriveInfo.GetDrives().First(d => d.IsReady).Name);
            }
            catch { }
        }

        private void LoadIndexingSettings()
        {
            try
            {
                if (File.Exists(_indexingSettingsPath))
                    _indexingSettings = JsonSerializer.Deserialize<IndexingSettings>(File.ReadAllText(_indexingSettingsPath)) ?? new IndexingSettings();
            }
            catch { _indexingSettings = new IndexingSettings(); }
        }

        private void LoadExclusionSettings()
        {
            try
            {
                if (File.Exists(_exclusionSettingsPath))
                    _exclusionSettings = JsonSerializer.Deserialize<ExclusionSettings>(File.ReadAllText(_exclusionSettingsPath)) ?? new ExclusionSettings();
            }
            catch { _exclusionSettings = new ExclusionSettings(); }
        }

        private void LoadGeneralSettings()
        {
            try
            {
                if (File.Exists(_generalSettingsPath))
                    _generalSettings = JsonSerializer.Deserialize<GeneralSettings>(File.ReadAllText(_generalSettingsPath)) ?? new GeneralSettings();
            }
            catch { _generalSettings = new GeneralSettings(); }
        }

        private void LoadRecentFiles()
        {
            try
            {
                if (File.Exists(_recentFilesPath))
                    _recentFiles = JsonSerializer.Deserialize<List<SearchResult>>(File.ReadAllText(_recentFilesPath)) ?? new List<SearchResult>();
            }
            catch { }
        }

        private void LoadRecentSearches()
        {
            try
            {
                if (File.Exists(_recentSearchesPath))
                    _recentSearches = JsonSerializer.Deserialize<List<SearchResult>>(File.ReadAllText(_recentSearchesPath)) ?? new List<SearchResult>();
            }
            catch { }
        }

        public void SaveRecentFiles()
        {
            File.WriteAllText(_recentFilesPath, JsonSerializer.Serialize(_recentFiles.Take(MaxRecentFiles).ToList()));
        }

        public void SaveRecentSearches()
        {
            File.WriteAllText(_recentSearchesPath, JsonSerializer.Serialize(_recentSearches.Take(MaxRecentSearches).ToList()));
        }

        public void SaveIndexingSettings() => File.WriteAllText(_indexingSettingsPath, JsonSerializer.Serialize(_indexingSettings));
        public void SaveExclusionSettings() => File.WriteAllText(_exclusionSettingsPath, JsonSerializer.Serialize(_exclusionSettings));
        public void SaveGeneralSettings() => File.WriteAllText(_generalSettingsPath, JsonSerializer.Serialize(_generalSettings));

        #endregion

        #region UI Updates

        public void UpdateDefaultView()
        {
            var displayList = new List<SearchResult>();

            if (_recentFiles.Any())
            {
                displayList.AddRange(_recentFiles.Select(r => { r.GroupName = "Recently Opened"; return r; }));
            }

            if (_recentSearches.Any())
            {
                displayList.AddRange(_recentSearches.Select(s => { s.GroupName = "Recent Searches"; return s; }));
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
            string query = SearchText ?? string.Empty;
            string lowerQuery = query.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(lowerQuery))
            {
                UpdateDefaultView();
                return;
            }

            var candidates = _fileSystemIndex
                .Where(r => !string.IsNullOrEmpty(r.Name) &&
                            r.Name.IndexOf(lowerQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            // ✅ If no local results → fallback to web search
            if (!candidates.Any())
            {
                var webResult = new SearchResult
                {
                    Name = $"Search for \"{query}\" on the Web",
                    FullPath = $"WEBSEARCH:{query}",
                    Type = ResultType.WebSearch,   // proper type
                    IsSpecialCommand = false,      // allow deletion
                    GroupName = "Recent Searches"
                };

                _groupedResults.Source = new List<SearchResult> { webResult };
                OnPropertyChanged(nameof(GroupedResults));
                return;
            }

            // Ranking and filtering
            IEnumerable<SearchResult> rankedResults = candidates
                .Select(r => new { Result = r, Score = ComputeScore(r, lowerQuery) })
                .OrderByDescending(x => x.Score)
                .Select(x => x.Result);

            if (GeneralSettings.CleanMode)
            {
                rankedResults = rankedResults
                    .Where(r => !CleanModeNoisyExtensions.Contains(Path.GetExtension(r.FullPath)))
                    .Take(10);
            }

            var grouped = rankedResults.GroupBy(r => r.GroupName ?? "Results").SelectMany(g => g);
            _groupedResults.Source = grouped.ToList();
            OnPropertyChanged(nameof(GroupedResults));
        }

        private int ComputeScore(SearchResult result, string query)
        {
            if (result == null || string.IsNullOrEmpty(result.Name)) return 0;
            int score = 0;

            if (result.Name.Equals(query, StringComparison.OrdinalIgnoreCase)) score += 1000;
            if (result.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) score += 500;
            if (result.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) score += 100;

            score += result.LaunchCount * 10;
            return score;
        }

        #endregion

        #region Indexing

        public async Task BuildFileSystemIndexAsync(bool forceRebuild)
        {
            IsIndexingInProgress = true;
            IsLoadingFromCache = !forceRebuild;

            try
            {
                if (!forceRebuild && File.Exists(_indexFilePath))
                {
                    _fileSystemIndex = JsonSerializer.Deserialize<List<SearchResult>>(File.ReadAllText(_indexFilePath)) ?? new List<SearchResult>();
                    IndexingStatusText = $"{_fileSystemIndex.Count} items loaded from cache";
                }
                else
                {
                    _fileSystemIndex = await FileIndexer.BuildIndexAsync(SearchScope, ExclusionSettings, progress =>
                    {
                        IndexingStatusText = $"{progress} items indexed...";
                    });
                    File.WriteAllText(_indexFilePath, JsonSerializer.Serialize(_fileSystemIndex));
                    IndexingStatusText = $"{_fileSystemIndex.Count} items indexed";
                }
            }
            catch (Exception ex)
            {
                IndexingStatusText = $"Indexing failed: {ex.Message}";
            }
            finally
            {
                IsIndexingInProgress = false;
                IsLoadingFromCache = false;
            }
        }

        #endregion

        #region Recents Management

        public void AddToRecentFiles(SearchResult result)
        {
            _recentFiles.RemoveAll(r => r.FullPath == result.FullPath);
            _recentFiles.Insert(0, result);
            if (_recentFiles.Count > MaxRecentFiles)
                _recentFiles.RemoveAt(_recentFiles.Count - 1);
            SaveRecentFiles();
        }

        public void AddToRecentSearches(SearchResult result)
        {
            _recentSearches.RemoveAll(r => r.FullPath == result.FullPath);
            _recentSearches.Insert(0, result);
            if (_recentSearches.Count > MaxRecentSearches)
                _recentSearches.RemoveAt(_recentSearches.Count - 1);
            SaveRecentSearches();
        }

        #endregion
    }
}
