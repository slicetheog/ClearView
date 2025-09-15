// FILE: Logic/Indexer.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClearView.Data;

namespace ClearView.Logic
{
    public class Indexer : IDisposable
    {
        private readonly string _cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearView", "fileSystemIndex.json");
        private static readonly HashSet<string> WhitelistExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Documents
            ".txt", ".md", ".rtf", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            // Images
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".webp",
            // Audio/Video
             ".mp3", ".wav", ".ogg", ".m4a", ".mp4", ".mkv", ".mov", ".avi",
            // Source / code
            ".cs", ".js", ".ts", ".java", ".py", ".cpp", ".c", ".h", ".css", ".html", ".json", ".xml",
            // Archives / packages (common)
            ".zip", ".tar", ".gz", ".7z", ".rar",
            // Shortcuts / executables: include as 
            ".exe", ".lnk"
        };
        
        public async Task<List<SearchResult>> BuildIndexAsync(List<string> drives, ExclusionSettings exclusionSettings, SearchMode searchMode, IProgress<long> progress)
        {
            var results = new ConcurrentBag<SearchResult>();
            long processedCount = 0;

            // CHANGED: Also scan Start Menu folders to reliably find applications.
            var pathsToIndex = new List<string>(drives);
            pathsToIndex.Add(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu));
            pathsToIndex.Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu));
            pathsToIndex = pathsToIndex.Distinct().ToList();


            await Task.Run(() =>
            {
                Parallel.ForEach(pathsToIndex, path => // CHANGED: Use combined list of paths.
                {
                    if (Directory.Exists(path))
                    {
                         IndexDirectory(path, results, exclusionSettings, searchMode, progress, ref processedCount);
                    }
                });
            });

            var finalResults = results.ToList();
            await SaveIndexToCacheAsync(finalResults);
            return finalResults;
        }

        private void IndexDirectory(string path, ConcurrentBag<SearchResult> results, ExclusionSettings exclusionSettings, SearchMode searchMode, IProgress<long> progress, ref long processedCount)
        {
            if (exclusionSettings.IsExcluded(path)) return;
            
            try
            {
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    if (exclusionSettings.IsExcluded(file)) continue;

                    if (searchMode == SearchMode.Clean)
                    {
                        try
                        {
                            var attrs = File.GetAttributes(file);
                            if ((attrs & FileAttributes.Hidden) == FileAttributes.Hidden) continue;
                            if ((attrs & FileAttributes.System) == FileAttributes.System) continue;
                        }
                        catch
                        {
                            continue;
                        }

                        var ext = Path.GetExtension(file);
                        if (string.IsNullOrEmpty(ext) || !WhitelistExtensions.Contains(ext))
                        {
                            continue;
                        }
                    }

                    results.Add(new SearchResult { Name = Path.GetFileName(file), FullPath = file, Type = GetResultType(file) });
                    System.Threading.Interlocked.Increment(ref processedCount);
                    if (processedCount % 1000 == 0)
                    {
                        progress.Report(processedCount);
                    }
                }

                foreach (var dir in Directory.EnumerateDirectories(path))
                {
                    if (!exclusionSettings.IsExcluded(dir))
                    {
                        if (searchMode == SearchMode.Clean)
                        {
                            try
                            {
                                var attrs = File.GetAttributes(dir);
                                if ((attrs & FileAttributes.Hidden) == FileAttributes.Hidden) continue;
                                if ((attrs & FileAttributes.System) == FileAttributes.System) continue;
                            }
                            catch
                            {
                                continue;
                            }
                        }

                        results.Add(new SearchResult { Name = Path.GetFileName(dir), FullPath = dir, Type = ResultType.Folder });
                        IndexDirectory(dir, results, exclusionSettings, searchMode, progress, ref processedCount);
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }
            catch { }
        }

   
        private ResultType GetResultType(string path)
        {
            string extension = Path.GetExtension(path)?.ToLowerInvariant()!;
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