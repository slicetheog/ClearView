using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ClearView.Data; // CHANGED: Added to use GeneralSettings
using ClearView.UI;

namespace ClearView
{
    public class HotkeyHelper
    {
        private const int HOTKEY_ID = 9000;
        private readonly Window _window;
        private IntPtr _hWnd;
        private HwndSource? _source;

        // CHANGED: The hotkey is now passed in from the settings.
        private Key _key;
        private ModifierKeys _modifiers;

        public HotkeyHelper(Window window, GeneralSettings settings)
        {
            _window = window;
            _key = settings.HotkeyKey;
            _modifiers = settings.HotkeyModifiers;
        }

        public void Register()
        {
            _hWnd = new WindowInteropHelper(_window).Handle;
            _source = HwndSource.FromHwnd(_hWnd);
            _source?.AddHook(HwndHook);

            RegisterHotKey(_hWnd, HOTKEY_ID, (uint)_modifiers, (uint)KeyInterop.VirtualKeyFromKey(_key));
        }

        public void Unregister()
        {
            _source?.RemoveHook(HwndHook);
            UnregisterHotKey(_hWnd, HOTKEY_ID);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                if (_window is LauncherWindow launcher)
                {
                    launcher.ShowLauncher();
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}