using System.Threading;
using System.Windows;

namespace SpotlightClean
{
    public partial class App : System.Windows.Application
    {
        private Mutex _mutex;

        public App()
        {
            _mutex = new Mutex(true, "SpotlightClean", out bool createdNew);
            if (!createdNew)
            {
                Current.Shutdown();
            }
        }
        
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // For debugging
            System.Windows.MessageBox.Show("An unhandled exception just occurred: " + e.Exception.Message, "Exception Sample", MessageBoxButton.OK, MessageBoxImage.Warning); // CHANGED: Fixed ambiguous reference
            e.Handled = true;
        }
    }
}