using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SpotlightClean.Data;

namespace SpotlightClean.Logic
{
    public class Indexer : IDisposable
    {
        private readonly string _cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpotlightClean", "fileSystemIndex.json");

        public async Task<List<SearchResult>> BuildIndexAsync(List<string> drives, ExclusionSettings exclusionSettings, IProgress<long> progress)
        {
            var results = new ConcurrentBag<SearchResult>();
            long processedCount = 0;

            await Task.Run(() =>
            {
                Parallel.ForEach(drives, drive =>
                {
                    if (Directory.Exists(drive))
                    {
                        IndexDirectory(drive, results, exclusionSettings, progress, ref processedCount);
                    }
                });
            });

            var finalResults = results.ToList();
            await SaveIndexToCacheAsync(finalResults);
            return finalResults;
        }

        private void IndexDirectory(string path, ConcurrentBag<SearchResult> results, ExclusionSettings exclusionSettings, IProgress<long> progress, ref long processedCount)
        {
            if (exclusionSettings.IsExcluded(path)) return;

            try
            {
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    if (!exclusionSettings.IsExcluded(file))
                    {
                        results.Add(new SearchResult { Name = Path.GetFileName(file), FullPath = file, Type = GetResultType(file) });
                        System.Threading.Interlocked.Increment(ref processedCount);
                        if (processedCount % 1000 == 0)
                        {
                            progress.Report(processedCount);
                        }
                    }
                }

                foreach (var dir in Directory.EnumerateDirectories(path))
                {
                    if (!exclusionSettings.IsExcluded(dir))
                    {
                        results.Add(new SearchResult { Name = Path.GetFileName(dir), FullPath = dir, Type = ResultType.Folder });
                        IndexDirectory(dir, results, exclusionSettings, progress, ref processedCount);
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }
        }

        private ResultType GetResultType(string path)
        {
            string extension = Path.GetExtension(path)?.ToLowerInvariant()!; // CHANGED: Added null-forgiving operator to resolve warning.
            if (extension == ".exe" || extension == ".lnk")
            {
                return ResultType.Application;
            }
            return ResultType.File;
        }

        public async Task SaveIndexToCacheAsync(List<SearchResult> index)
        {
            string json = JsonSerializer.Serialize(index);
            await File.WriteAllTextAsync(_cachePath, json);
        }

        public async Task<List<SearchResult>> LoadIndexFromCacheAsync()
        {
            if (File.Exists(_cachePath))
            {
                string json = await File.ReadAllTextAsync(_cachePath);
                return JsonSerializer.Deserialize<List<SearchResult>>(json) ?? new List<SearchResult>();
            }
            return new List<SearchResult>();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}