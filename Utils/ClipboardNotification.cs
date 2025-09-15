// FILE: Utils/ClipboardNotification.cs
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClearView.Utils
{
    public static class ClipboardNotification
    {
        // Event that fires when clipboard changes
        public static event EventHandler? ClipboardUpdate; // CHANGED: Made event nullable to resolve CS8618 warning.
        private static HwndSource? _source; // CHANGED: Made field nullable to resolve CS8618 warning.
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        public static void Start()
        {
            if (_source != null) return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var parameters = new HwndSourceParameters("ClipboardNotificationWindow")
                {
                    ParentWindow = HWND_MESSAGE,
                    WindowStyle = 0,
                    Width = 0,
                    Height = 0
                };

                _source = new HwndSource(parameters);
                _source.AddHook(WndProc);

                AddClipboardFormatListener(_source.Handle);
            });
        }

        public static void Stop()
        {
            if (_source == null) return;
            RemoveClipboardFormatListener(_source.Handle);
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                ClipboardUpdate?.Invoke(null, EventArgs.Empty);
                handled = true;
            }
            return IntPtr.Zero;
        }
    }
}