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
        private const string SingleInstanceActivateEventName = "Global\\XPhyWpfUi_SingleInstance_Activate";
        private static Mutex _singleInstanceMutex;
        private static Thread _activateListenerThread;
        private static volatile bool _activateListenerShutdown;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Single instance: only allow one running instance. If another is already running, signal it to show and exit.
            bool createdNew = false;
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);
            try
            {
                if (!createdNew)
                {
                    _singleInstanceMutex?.Dispose();
                    _singleInstanceMutex = null;
                    SignalExistingInstanceToActivate();
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

            // When user clicks desktop icon while app is in tray, the new process signals us via this event. Listen so we can show the window.
            StartActivateListener();

            // If SessionExpiredException is thrown (e.g. expired tokens, API 401), clear tokens and show sign-in instead of crashing.
            Application.Current.Dispatcher.UnhandledException += Dispatcher_UnhandledException;

            // Load user's saved theme preference (Dark/Light mode)
            ThemeManager.LoadSavedTheme();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _activateListenerShutdown = true;
            try { _activateEvent?.Set(); } catch { }
            try { _activateEvent?.Dispose(); } catch { }
            _activateEvent = null;
            base.OnExit(e);
        }

        private static EventWaitHandle _activateEvent;

        private static void StartActivateListener()
        {
            try
            {
                _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, SingleInstanceActivateEventName);
                _activateListenerShutdown = false;
                _activateListenerThread = new Thread(() =>
                {
                    while (!_activateListenerShutdown && _activateEvent != null)
                    {
                        try
                        {
                            if (_activateEvent.WaitOne(500))
                            {
                                if (_activateListenerShutdown) break;
                                var app = Current;
                                if (app != null)
                                    app.Dispatcher.Invoke(() => BringMainWindowToFront());
                            }
                        }
                        catch (ObjectDisposedException) { break; }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Activate listener: {ex.Message}"); }
                    }
                })
                { IsBackground = true };
                _activateListenerThread.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartActivateListener: {ex.Message}");
            }
        }

        private static void BringMainWindowToFront()
        {
            try
            {
                var main = Current?.MainWindow as MainWindow;
                if (main != null)
                {
                    main.Show();
                    main.WindowState = WindowState.Normal;
                    main.Activate();
                    // If user closed with (X) and Remember Me was false, tokens were cleared; show Welcome instead of Home
                    main.EnsureViewMatchesAuthStateAfterRestore();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BringMainWindowToFront: {ex.Message}");
            }
        }

        /// <summary>Signal the already-running instance to show its window. Used when user clicks desktop icon and we are the second process.</summary>
        private static void SignalExistingInstanceToActivate()
        {
            try
            {
                using (var ev = EventWaitHandle.OpenExisting(SingleInstanceActivateEventName))
                    ev.Set();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SignalExistingInstanceToActivate: {ex.Message}");
                // Fallback: try bringing by window handle (works when window is minimized to taskbar, not when hidden to tray)
                BringExistingInstanceToFront();
            }
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

        /// <summary>Fallback when event signal fails: try to restore by main window handle (works if window is minimized, not when hidden to tray).</summary>
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
