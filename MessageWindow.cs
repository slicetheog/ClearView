using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace ClearView
{
    public class MessageWindow : Form
    {
        private const int HOTKEY_ID = 9000;
        private const uint WM_HOTKEY = 0x0312;

        public event Action? HotkeyPressed;

        public MessageWindow()
        {
            CreateHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                if ((int)m.WParam == HOTKEY_ID)
                {
                    HotkeyPressed?.Invoke();
                }
            }
            base.WndProc(ref m);
        }
    }
}