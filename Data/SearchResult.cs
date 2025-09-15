// FILE: Data/SearchResult.cs
using System;

namespace ClearView.Data
{
    public enum ResultType
    {
        Application,
        File,
        Folder,
        Calculator,
        Url,
        Clipboard,       // ✅ keep this
        Command,         // ✅ keep this
        WebSearch,       // ✅ new
        RecentWebSearch  // ✅ new
    }

    public class SearchResult
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public ResultType Type { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public bool IsSpecialCommand { get; set; } = false; // reserved only for Exit/Settings/etc
        public int LaunchCount { get; set; } = 0;
    }
}