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

        // Conservative whitelist for Clean mode. Add/remove here as needed by code (not UI).
        // Contains common document, image, audio, video, and common source file extensions.
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
            // Shortcuts / executables: include as apps (kept for convenience)
            ".exe", ".lnk"
        };

        public async Task<List<SearchResult>> BuildIndexAsync(List<string> drives, ExclusionSettings exclusionSettings, SearchMode searchMode, IProgress<long> progress)
        {
            var results = new ConcurrentBag<SearchResult>();
            long processedCount = 0;

            await Task.Run(() =>
            {
                Parallel.ForEach(drives, drive =>
                {
                    if (Directory.Exists(drive))
                    {
                        IndexDirectory(drive, results, exclusionSettings, searchMode, progress, ref processedCount);
                    }
                });
            });

            var finalResults = results.ToList();
            await SaveIndexToCacheAsync(finalResults);
            return finalResults;
        }

        private void IndexDirectory(string path, ConcurrentBag<SearchResult> results, ExclusionSettings exclusionSettings, SearchMode searchMode, IProgress<long> progress, ref long processedCount)
        {
            // Respect exclusion settings first (folder-level)
            if (exclusionSettings.IsExcluded(path)) return;

            try
            {
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    // If exclusion settings say skip this file, honor it.
                    if (exclusionSettings.IsExcluded(file)) continue;

                    // In Clean mode apply conservative filters:
                    if (searchMode == SearchMode.Clean)
                    {
                        try
                        {
                            var attrs = File.GetAttributes(file);
                            // Skip hidden or system files
                            if ((attrs & FileAttributes.Hidden) == FileAttributes.Hidden) continue;
                            if ((attrs & FileAttributes.System) == FileAttributes.System) continue;
                        }
                        catch
                        {
                            // If we cannot read attributes, conservatively skip the file for Clean mode.
                            continue;
                        }

                        var ext = Path.GetExtension(file);
                        if (string.IsNullOrEmpty(ext) || !WhitelistExtensions.Contains(ext))
                        {
                            // Skip unusual/unwhitelisted extensions in Clean mode
                            continue;
                        }
                    }

                    // Add file (PowerHungry or Clean-passed)
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
                        // In Clean mode, skip hidden/system directories as well
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
                                // If attributes cannot be read, skip directory in Clean mode.
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
            catch { /* swallow other IO exceptions to keep indexing resilient */ }
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
