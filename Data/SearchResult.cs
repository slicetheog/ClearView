// ========================================================================
// FILE: Data/SearchResult.cs
// ========================================================================
namespace ClearView.Data
{
    public enum ResultType
    {
        File,
        Folder,
        Application,
        Calculator,
        WebSearch,
        RecentWebSearch,
        Url,
        Clipboard,
        Command
    }

    public class SearchResult
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public ResultType Type { get; set; }
        public int LaunchCount { get; set; }
        public bool IsSpecialCommand { get; set; } = false;
        public string GroupName { get; set; } = "";
        public string ClipboardText { get; set; } = "";
    }
}
