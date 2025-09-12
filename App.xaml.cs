// ========================================================================
// FILE: App.xaml.cs
// ========================================================================
using System.Threading;
using System.Windows;
using ClearView.Logic; // ClipboardHistoryManager
using ClearView.Utils; // ClipboardNotification

namespace ClearView
{
    public partial class App : System.Windows.Application
    {
        private Mutex _mutex;

        public App()
        {
            _mutex = new Mutex(true, "ClearView", out bool createdNew);
            if (!createdNew)
            {
                Current.Shutdown();
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Clipboard listener
            try
            {
                ClipboardNotification.Start();
                ClipboardNotification.ClipboardUpdate += (s, args) =>
                {
                    try
                    {
                        if (System.Windows.Clipboard.ContainsText())
                        {
                            string text = System.Windows.Clipboard.GetText();
                            ClipboardHistoryManager.AddEntry(text);
                        }
                    }
                    catch { /* ignore access errors */ }
                };

                // Seed clipboard at startup
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                        ClipboardHistoryManager.AddEntry(System.Windows.Clipboard.GetText());
                }
                catch { }
            }
            catch { /* swallow exceptions */ }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show("An unhandled exception occurred: " + e.Exception.Message,
                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }
    }
}
