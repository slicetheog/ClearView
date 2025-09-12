using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SpotlightClean.Data
{
    public static class UsageAnalytics
    {
        private static readonly string _analyticsPath;
        private static Dictionary<string, int> _launchCounts;

        static UsageAnalytics()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpotlightClean");
            _analyticsPath = Path.Combine(appDataPath, "usage_analytics.json");
            _launchCounts = LoadAnalytics();
        }

        private static Dictionary<string, int> LoadAnalytics()
        {
            try
            {
                if (File.Exists(_analyticsPath))
                {
                    string json = File.ReadAllText(_analyticsPath);
                    return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                }
            }
            catch { /* Ignore errors, start fresh */ }
            return new Dictionary<string, int>();
        }

        public static void SaveAnalytics()
        {
            try
            {
                string? directory = Path.GetDirectoryName(_analyticsPath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                    string json = JsonSerializer.Serialize(_launchCounts);
                    File.WriteAllText(_analyticsPath, json);
                }
            }
            catch { /* Ignore errors */ }
        }

        public static void IncrementLaunchCount(string fullPath)
        {
            if (_launchCounts.ContainsKey(fullPath))
            {
                _launchCounts[fullPath]++;
            }
            else
            {
                _launchCounts[fullPath] = 1;
            }
        }

        public static int GetLaunchCount(string fullPath)
        {
            return _launchCounts.GetValueOrDefault(fullPath, 0);
        }
    }
}

