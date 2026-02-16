using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui
{
    public partial class App : Application
    {
        private const string SingleInstanceMutexName = "Global\\XPhyWpfUi_SingleInstance_Mutex";
        private static Mutex _singleInstanceMutex;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Single instance: only allow one running instance. If another is already running, bring it to front and exit.
            bool createdNew = false;
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);
            try
            {
                if (!createdNew)
                {
                    _singleInstanceMutex?.Dispose();
                    _singleInstanceMutex = null;
                    BringExistingInstanceToFront();
                    Shutdown();
                    return;
                }
            }
            catch (AbandonedMutexException)
            {
                // Previous instance exited without releasing; we can take ownership and continue.
            }

            // If the user uninstalled and reinstalled, AppData (and tokens) survive uninstall.
            // Detect new install by exe identity and clear tokens so they must log in again.
            TokenStorage.ClearTokensIfNewInstall();

            // When the app is launched by the installer ("Launch when finished"), the process
            // current directory is often the installer's folder, not the app's. The native
            // video inference setup resolves the "models" path relative to the current directory.
            // Setting it here ensures models are always loaded from the application directory,
            // fixing "Environment Inference setup" failure on first run after install.
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(appDir) && Directory.Exists(appDir))
                {
                    Environment.CurrentDirectory = appDir;
                }
            }
            catch { /* non-fatal */ }

            base.OnStartup(e);

            // If SessionExpiredException is thrown (e.g. expired tokens, API 401), clear tokens and show sign-in instead of crashing.
            // Use Application.Current.Dispatcher so we catch exceptions from async continuations that resume on the UI thread.
            Application.Current.Dispatcher.UnhandledException += Dispatcher_UnhandledException;

            // Load user's saved theme preference (Dark/Light mode)
            ThemeManager.LoadSavedTheme();
        }

        private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Flatten so we catch SessionExpiredException even when wrapped in AggregateException (e.g. from async)
            var ex = e.Exception is AggregateException agg ? agg.InnerException : e.Exception;
            if (ex is SessionExpiredException)
            {
                try
                {
                    new TokenStorage().ClearTokens();
                    if (Application.Current?.MainWindow is MainWindow main)
                        main.ShowAuthViewIfNeeded();
                }
                catch (Exception handlerEx) { System.Diagnostics.Debug.WriteLine($"SessionExpired handler: {handlerEx.Message}"); }
                e.Handled = true;
            }
        }

        private static void BringExistingInstanceToFront()
        {
            try
            {
                string currentProcessName = Process.GetCurrentProcess().ProcessName;
                int currentPid = Process.GetCurrentProcess().Id;

                foreach (Process p in Process.GetProcessesByName(currentProcessName))
                {
                    if (p.Id == currentPid)
                        continue;

                    IntPtr hWnd = p.MainWindowHandle;
                    if (hWnd != IntPtr.Zero)
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                        SetForegroundWindow(hWnd);
                        break;
                    }
                }
            }
            catch { /* non-fatal: just exit this instance */ }
        }
    }
}
