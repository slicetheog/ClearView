using System.Windows.Input;

namespace SpotlightClean.Data
{
    // CHANGED: Added new settings class for general application settings.
    public class GeneralSettings
    {
        public bool RunOnStartup { get; set; } = false;
        public Key HotkeyKey { get; set; } = Key.Space;
        public ModifierKeys HotkeyModifiers { get; set; } = ModifierKeys.Control;
    }
}