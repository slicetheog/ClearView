using System.Windows.Input;

namespace ClearView.Data
{
    // CHANGED: Added new settings class for general application settings and SearchMode.
    public enum SearchMode
    {
        Clean = 0,
        PowerHungry = 1
    }

    public class GeneralSettings
    {
        public bool RunOnStartup { get; set; } = false;
        public Key HotkeyKey { get; set; } = Key.Space;
        public ModifierKeys HotkeyModifiers { get; set; } = ModifierKeys.Control;

        // New: user-visible mode selection. Default to Clean for new installs.
        public SearchMode SearchMode { get; set; } = SearchMode.Clean;
    }
}
