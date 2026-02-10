using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace InstallerUI
{
    /// <summary>
    /// ViewModel for the installer shell. Holds current step, install path, license accepted,
    /// and commands for Next/Back/Cancel. Also runs the MSI silently and reports success/failure.
    /// MSI dialogs are not used; this WPF app is the only UI. The MSI is executed via
    /// msiexec /i "path" /quiet /norestart INSTALLDIR="..." and we treat ExitCode 0 and 3010 as success.
    /// </summary>
    public class InstallerViewModel : INotifyPropertyChanged
    {
        private InstallerStep _currentStep = InstallerStep.Welcome;
        private string _installPath;
        private bool _licenseAccepted;
        private bool _eulaScrolledToBottom;
        private bool _isQuickInstall = true;
        private bool _launchOnStartup;
        private bool _launchNow = true; // Default: "Launch X-PHY Deepfake Detector now" checked
        private bool _isInstalling;
        private string _progressStage = "";
        private double _progressValue;
        private bool _installSucceeded;
        private string _failureMessage;
        private bool _canGoNext = true;
        private bool _canGoBack = true;
        private bool _msiComplete;
        private int _msiExitCode = -1;

        public InstallerViewModel()
        {
            _installPath = GetDefaultInstallPath();
            NextCommand = new RelayCommand(OnNext, () => CanGoNext);
            BackCommand = new RelayCommand(OnBack, () => CanGoBack);
            CancelCommand = new RelayCommand(OnCancel);
        }

        public InstallerStep CurrentStep
        {
            get => _currentStep;
            set { _currentStep = value; OnPropertyChanged(); UpdateButtonStates(); }
        }

        public string InstallPath
        {
            get => _installPath;
            set { _installPath = value; OnPropertyChanged(); }
        }

        public bool LicenseAccepted
        {
            get => _licenseAccepted;
            set { _licenseAccepted = value; OnPropertyChanged(); UpdateButtonStates(); }
        }

        /// <summary>True when user has scrolled to the bottom of the EULA; enables Next on License screen.</summary>
        public bool EulaScrolledToBottom
        {
            get => _eulaScrolledToBottom;
            set { _eulaScrolledToBottom = value; OnPropertyChanged(); UpdateButtonStates(); }
        }

        public bool IsQuickInstall
        {
            get => _isQuickInstall;
            set
            {
                _isQuickInstall = value;
                if (value) InstallPath = GetDefaultInstallPath();
                OnPropertyChanged(); UpdateButtonStates();
            }
        }

        public bool LaunchOnStartup
        {
            get => _launchOnStartup;
            set { _launchOnStartup = value; OnPropertyChanged(); }
        }

        public bool LaunchNow
        {
            get => _launchNow;
            set { _launchNow = value; OnPropertyChanged(); }
        }

        public bool IsInstalling
        {
            get => _isInstalling;
            set { _isInstalling = value; OnPropertyChanged(); UpdateButtonStates(); }
        }

        public string ProgressStage
        {
            get => _progressStage;
            set { _progressStage = value; OnPropertyChanged(); }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        public bool InstallSucceeded
        {
            get => _installSucceeded;
            set { _installSucceeded = value; OnPropertyChanged(); OnPropertyChanged(nameof(FinishButtonLabel)); }
        }

        public string FailureMessage
        {
            get => _failureMessage;
            set { _failureMessage = value; OnPropertyChanged(); }
        }

        public bool CanGoNext
        {
            get => _canGoNext;
            set { _canGoNext = value; OnPropertyChanged(); ((RelayCommand)NextCommand).RaiseCanExecuteChanged(); }
        }

        public bool CanGoBack
        {
            get => _canGoBack;
            set { _canGoBack = value; OnPropertyChanged(); ((RelayCommand)BackCommand).RaiseCanExecuteChanged(); }
        }

        public bool ShowBackButton => CurrentStep != InstallerStep.Welcome && CurrentStep != InstallerStep.Progress && CurrentStep != InstallerStep.Finish;
        public bool ShowNextButton => CurrentStep != InstallerStep.Finish && CurrentStep != InstallerStep.Progress;
        public bool ShowFinishButton => CurrentStep == InstallerStep.Finish;
        /// <summary>Hide Cancel on Finish screen so only Close is shown.</summary>
        public bool ShowCancelButton => CurrentStep != InstallerStep.Finish;
        public string NextButtonLabel => CurrentStep == InstallerStep.LicenseAgreement ? "I Accept" : "Next";
        /// <summary>On Finish screen: "Close" when error, "Finish" when success—so user can always exit.</summary>
        public string FinishButtonLabel => CurrentStep == InstallerStep.Finish && !InstallSucceeded ? "Close" : "Finish";

        public ICommand NextCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand CancelCommand { get; }

        public Action<object> OnNavigate { get; set; }
        public Action OnRequestClose { get; set; }

        /// <summary>Get 64-bit Program Files root (e.g. C:\Program Files) even when running as 32-bit process.</summary>
        private static string GetProgramFilesRoot64()
        {
            try
            {
                if (Environment.Is64BitOperatingSystem)
                {
                    using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                        ?.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion"))
                    {
                        var dir = key?.GetValue("ProgramFilesDir") as string;
                        if (!string.IsNullOrEmpty(dir)) return dir.TrimEnd('\\', '/');
                    }
                }
            }
            catch { }
            return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\', '/');
        }

        /// <summary>Get Program Files (x86) root (e.g. C:\Program Files (x86)).</summary>
        private static string GetProgramFilesRootX86()
        {
            try
            {
                var x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (!string.IsNullOrEmpty(x86)) return x86.TrimEnd('\\', '/');
            }
            catch { }
            return null;
        }

        /// <summary>Default install path: prefer 64-bit Program Files when OS is 64-bit (app is x64); otherwise use current process Program Files.</summary>
        private static string GetDefaultInstallPath()
        {
            var root = GetProgramFilesRoot64();
            if (!string.IsNullOrEmpty(root))
                return Path.Combine(root, "X-PHY", "X-PHY Deepfake Detector");
            var fallback = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return Path.Combine(fallback, "X-PHY", "X-PHY Deepfake Detector");
        }

        private static string GetDefaultInstallPathX86()
        {
            var root = GetProgramFilesRootX86();
            if (string.IsNullOrEmpty(root)) return null;
            return Path.Combine(root, "X-PHY", "X-PHY Deepfake Detector");
        }

        /// <summary>Returns candidate install directories to try when launching (InstallPath first, then Program Files variants, then parent dir).</summary>
        public static string[] GetCandidateInstallDirectories(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath))
                return Array.Empty<string>();
            var normalized = installPath.TrimEnd('\\', '/');
            var list = new List<string> { normalized };
            var pf64 = GetProgramFilesRoot64();
            var pf86 = GetProgramFilesRootX86();
            void AddIfNew(string path)
            {
                if (string.IsNullOrEmpty(path)) return;
                path = path.TrimEnd('\\', '/');
                if (!list.Contains(path, StringComparer.OrdinalIgnoreCase)) list.Add(path);
            }
            AddIfNew(Path.Combine(pf64, "X-PHY", "X-PHY Deepfake Detector"));
            AddIfNew(Path.Combine(pf86 ?? "", "X-PHY", "X-PHY Deepfake Detector"));
            AddIfNew(Path.Combine(pf64, "X-PHY"));
            AddIfNew(Path.Combine(pf86 ?? "", "X-PHY"));
            var parent = Path.GetDirectoryName(normalized);
            AddIfNew(parent);
            return list.ToArray();
        }

        /// <summary>Find installed exe path by trying InstallPath and then alternate Program Files locations. Returns (installDir, exePath) or (null, null).</summary>
        public static (string installDir, string exePath) TryResolveInstalledExePath(string installPath)
        {
            foreach (var dir in GetCandidateInstallDirectories(installPath))
            {
                var exePath = Path.Combine(dir, AppExeName);
                if (File.Exists(exePath)) return (dir, exePath);
            }
            return (null, null);
        }

        private const string AppExeName = "x_phy_wpf_ui.exe";

        /// <summary>True if the application is already installed (known paths, registry, or exe found on system).</summary>
        public static bool IsAlreadyInstalled()
        {
            if (IsAlreadyInstalledAt(GetDefaultInstallPath())) return true;
            var x86 = GetDefaultInstallPathX86();
            if (!string.IsNullOrEmpty(x86) && IsAlreadyInstalledAt(x86)) return true;
            if (IsAlreadyInstalledViaRegistry()) return true;
            if (IsAlreadyInstalledScanProgramFiles()) return true;
            if (IsAlreadyInstalledViaRunningProcess()) return true;
            // Installer running from same folder as app (e.g. portable or re-run from install dir)
            try
            {
                var installerDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
                if (!string.IsNullOrEmpty(installerDir) && IsAlreadyInstalledAt(installerDir)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>If our app process is running, its path is a valid install location.</summary>
        private static bool IsAlreadyInstalledViaRunningProcess()
        {
            try
            {
                var procName = Path.GetFileNameWithoutExtension(AppExeName);
                foreach (var proc in Process.GetProcessesByName(procName))
                {
                    try
                    {
                        var dir = Path.GetDirectoryName(proc.MainModule?.FileName);
                        if (!string.IsNullOrEmpty(dir) && IsAlreadyInstalledAt(dir))
                        {
                            proc.Dispose();
                            return true;
                        }
                    }
                    catch { /* access denied etc */ }
                    finally { proc.Dispose(); }
                }
            }
            catch { }
            return false;
        }

        /// <summary>Scan Program Files and Program Files (x86) for X-PHY folder containing the exe (fallback if registry differs).</summary>
        private static bool IsAlreadyInstalledScanProgramFiles()
        {
            try
            {
                foreach (var root in new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                })
                {
                    if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                    var xphyDir = Path.Combine(root, "X-PHY");
                    if (!Directory.Exists(xphyDir)) continue;
                    foreach (var subDir in Directory.GetDirectories(xphyDir))
                    {
                        if (IsAlreadyInstalledAt(subDir)) return true;
                    }
                    if (IsAlreadyInstalledAt(xphyDir)) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>Find if app is installed by scanning Uninstall registry for any InstallLocation containing our exe (no DisplayName dependency).</summary>
        private static bool IsAlreadyInstalledViaRegistry()
        {
            var views = new[] { RegistryView.Registry64, RegistryView.Registry32 };
            foreach (var view in views)
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (baseKey == null) continue;
                        foreach (var subKeyName in baseKey.GetSubKeyNames())
                        {
                            try
                            {
                                using (var subKey = baseKey.OpenSubKey(subKeyName))
                                {
                                    if (subKey == null) continue;
                                    var installLocation = subKey.GetValue("InstallLocation") as string;
                                    if (string.IsNullOrWhiteSpace(installLocation)) continue;
                                    var dir = installLocation.TrimEnd('\\', '/');
                                    if (IsAlreadyInstalledAt(dir)) return true;
                                }
                            }
                            catch { /* skip bad subkey */ }
                        }
                    }
                }
                catch { /* skip this view (e.g. 32-bit process can't open Registry64) */ }
            }
            try
            {
                using (var baseKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (baseKey != null)
                        foreach (var subKeyName in baseKey.GetSubKeyNames())
                        {
                            try
                            {
                                using (var subKey = baseKey.OpenSubKey(subKeyName))
                                {
                                    if (subKey == null) continue;
                                    var installLocation = subKey.GetValue("InstallLocation") as string;
                                    if (string.IsNullOrWhiteSpace(installLocation)) continue;
                                    var dir = installLocation.TrimEnd('\\', '/');
                                    if (IsAlreadyInstalledAt(dir)) return true;
                                }
                            }
                            catch { }
                        }
                }
            }
            catch { }
            return false;
        }

        /// <summary>True if the given directory exists and already contains the app exe (do not allow install over it).</summary>
        public static bool IsAlreadyInstalledAt(string installDir)
        {
            if (string.IsNullOrWhiteSpace(installDir)) return false;
            var dir = installDir.TrimEnd('\\', '/');
            if (!Directory.Exists(dir)) return false;
            var exePath = Path.Combine(dir, AppExeName);
            return File.Exists(exePath);
        }

        private void UpdateButtonStates()
        {
            OnPropertyChanged(nameof(ShowBackButton));
            OnPropertyChanged(nameof(ShowNextButton));
            OnPropertyChanged(nameof(ShowFinishButton));
            OnPropertyChanged(nameof(ShowCancelButton));
            OnPropertyChanged(nameof(NextButtonLabel));
            OnPropertyChanged(nameof(FinishButtonLabel));
            switch (CurrentStep)
            {
                case InstallerStep.Welcome:
                    CanGoNext = true;
                    CanGoBack = false;
                    break;
                case InstallerStep.LicenseAgreement:
                    CanGoNext = EulaScrolledToBottom;
                    CanGoBack = true;
                    break;
                case InstallerStep.InstallPath:
                    CanGoNext = IsQuickInstall || !string.IsNullOrWhiteSpace(InstallPath);
                    CanGoBack = true;
                    break;
                case InstallerStep.Progress:
                    CanGoNext = false;
                    CanGoBack = false;
                    break;
                case InstallerStep.Finish:
                    CanGoNext = true;
                    CanGoBack = false;
                    break;
            }
        }

        private void OnNext(object _)
        {
            // On every Next click, re-check: if app is already installed anywhere, block and show error (don't mislead user)
            if (CurrentStep != InstallerStep.Finish && CurrentStep != InstallerStep.Progress && IsAlreadyInstalled())
            {
                FailureMessage = "X-PHY Deepfake Detector is already installed. Please uninstall the existing version from Settings > Apps before running setup again.";
                InstallSucceeded = false;
                CurrentStep = InstallerStep.Finish;
                OnNavigate?.Invoke(InstallerStep.Finish);
                return;
            }

            if (CurrentStep == InstallerStep.InstallPath)
            {
                // Do not allow install if app is already installed at the selected path (or default)
                if (IsAlreadyInstalledAt(InstallPath))
                {
                    FailureMessage = "X-PHY Deepfake Detector is already installed at the selected location. Please uninstall the existing version from Settings > Apps before running setup again, or choose a different folder.";
                    InstallSucceeded = false;
                    CurrentStep = InstallerStep.Finish;
                    OnNavigate?.Invoke(InstallerStep.Finish);
                    return;
                }
                CurrentStep = InstallerStep.Progress;
                OnNavigate?.Invoke(CurrentStep);
                StartMsiInstall();
                return;
            }

            if (CurrentStep == InstallerStep.Finish)
            {
                // Finish button Click handler in MainWindow does launch + close (single place)
                return;
            }

            var next = CurrentStep switch
            {
                InstallerStep.Welcome => InstallerStep.LicenseAgreement,
                InstallerStep.LicenseAgreement => InstallerStep.InstallPath,
                _ => CurrentStep
            };
            CurrentStep = next;
            OnNavigate?.Invoke(CurrentStep);
        }

        private void OnBack(object _)
        {
            var prev = CurrentStep switch
            {
                InstallerStep.LicenseAgreement => InstallerStep.Welcome,
                InstallerStep.InstallPath => InstallerStep.LicenseAgreement,
                _ => CurrentStep
            };
            CurrentStep = prev;
            if (CurrentStep == InstallerStep.LicenseAgreement)
                EulaScrolledToBottom = false;
            OnNavigate?.Invoke(CurrentStep);
        }

        private void OnCancel(object _)
        {
            string message;
            string title = "Close installer";
            if (IsInstalling)
            {
                message = "Installation is in progress. Do you want to exit anyway?";
            }
            else
            {
                message = "Do you want to close the installer? The installation will not be completed.";
            }
            if (MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                OnRequestClose?.Invoke();
        }

        /// <summary>
        /// Run the MSI silently. We do not use MSI dialogs—the user only sees this WPF app.
        /// Locate the MSI next to InstallerUI.exe. Execute: msiexec /i "path" /quiet /norestart INSTALLDIR="..."
        /// ExitCode 0 = success; 3010 = success but restart required. Any other code = failure.
        /// </summary>
        private void StartMsiInstall()
        {
            IsInstalling = true;
            ProgressValue = 0;
            ProgressStage = "Preparing...";

            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            // MSI is typically next to the installer exe (e.g. X-PHY-Setup-WPF-UI-CPU.msi or similar).
            var msiPath = Path.Combine(exeDir, "X-PHY-Setup-WPF-UI-CPU.msi");
            if (!File.Exists(msiPath))
                msiPath = Path.Combine(exeDir, "X-PHY-Setup.msi");
            if (!File.Exists(msiPath))
            {
                FinishWithFailure("Setup package not found. Expected X-PHY-Setup-WPF-UI-CPU.msi or X-PHY-Setup.msi next to the installer.");
                return;
            }

            var args = $"/i \"{msiPath}\" /quiet /norestart INSTALLDIR=\"{InstallPath.TrimEnd('\\')}\"";

            var psi = new ProcessStartInfo("msiexec.exe")
            {
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Simulated progress stages on UI thread so user sees progress while MSI runs in background.
            var dispatcher = Dispatcher.CurrentDispatcher;
            var progressTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            var step = 0;
            progressTimer.Tick += (s, e) =>
            {
                step++;
                if (step <= 15) { ProgressStage = "Preparing..."; ProgressValue = step * 2.2; }
                else if (step <= 55) { ProgressStage = "Installing..."; ProgressValue = 33 + (step - 15) * 1.425; }
                else { ProgressStage = "Finalizing..."; ProgressValue = Math.Min(100, 90 + (step - 55) * 0.5); }

                // Only show End screen when progress has reached 100%.
                if (ProgressValue >= 100)
                {
                    progressTimer.Stop();
                    ProgressValue = 100;
                    ProgressStage = "Complete.";
                    IsInstalling = false;
                    if (_msiComplete)
                    {
                        if (_msiExitCode == 0 || _msiExitCode == 3010)
                        {
                            InstallSucceeded = true;
                            ApplyLaunchOnStartup();
                            CurrentStep = InstallerStep.Finish;
                            OnNavigate?.Invoke(InstallerStep.Finish);
                        }
                        else
                            FinishWithFailure($"Installation failed (exit code: {_msiExitCode}). Please try again or contact support.");
                    }
                }
            };
            progressTimer.Start();

            try
            {
                var process = Process.Start(psi);
                if (process == null)
                {
                    progressTimer.Stop();
                    FinishWithFailure("Could not start the installer process.");
                    return;
                }

                // Run WaitForExit on background thread. When MSI completes, only set _msiComplete; do not navigate until progress reaches 100%.
                Task.Run(() =>
                {
                    process.WaitForExit();
                    var exitCode = process.ExitCode;
                    process.Dispose();

                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        _msiComplete = true;
                        _msiExitCode = exitCode;
                        // If progress already reached 100%, navigate to Finish now.
                        if (ProgressValue >= 100)
                        {
                            progressTimer.Stop();
                            ProgressValue = 100;
                            ProgressStage = "Complete.";
                            IsInstalling = false;
                            if (exitCode == 0 || exitCode == 3010)
                            {
                                InstallSucceeded = true;
                                ApplyLaunchOnStartup();
                                CurrentStep = InstallerStep.Finish;
                                OnNavigate?.Invoke(InstallerStep.Finish);
                            }
                            else
                                FinishWithFailure($"Installation failed (exit code: {exitCode}). Please try again or contact support.");
                        }
                    }), DispatcherPriority.Normal);
                });
            }
            catch (Exception ex)
            {
                progressTimer.Stop();
                IsInstalling = false;
                FinishWithFailure($"Could not start installation: {ex.Message}");
            }
        }

        private void FinishWithFailure(string message)
        {
            FailureMessage = message;
            InstallSucceeded = false;
            CurrentStep = InstallerStep.Finish;
            OnNavigate?.Invoke(InstallerStep.Finish);
        }

        private void LaunchInstalledApp()
        {
            try
            {
                var (installDir, exePath) = TryResolveInstalledExePath(InstallPath);
                if (installDir == null || exePath == null) return;
                Process.Start(new ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = installDir
                });
            }
            catch { /* ignore */ }
        }

        private const string RunKeyName = "X-PHY Deepfake Detector";

        /// <summary>Apply launch-on-startup: add or remove from the Run key so the app runs when Windows starts.
        /// Writes to the interactive user's registry when the installer runs elevated (Admin), so it works for the user who will log in.</summary>
        public void ApplyLaunchOnStartup()
        {
            if (!InstallSucceeded) return;
            string exePath = null;
            if (LaunchOnStartup)
            {
                var (_, path) = TryResolveInstalledExePath(InstallPath);
                if (path == null || !File.Exists(path)) return;
                exePath = "\"" + path + "\"";
            }

            // Prefer interactive user's Run key (so when installer runs as Admin we write to the logged-in user, not Administrator)
            using (var key = InteractiveUserRunKey.OpenInteractiveUserRunKey(writable: true))
            {
                if (key != null)
                {
                    try
                    {
                        if (LaunchOnStartup)
                            key.SetValue(RunKeyName, exePath);
                        else
                        {
                            try { key.DeleteValue(RunKeyName, false); } catch { /* value may not exist */ }
                        }
                    }
                    catch { /* ignore */ }
                    return;
                }
            }

            // Fallback: current process user (correct when installer is not elevated)
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (LaunchOnStartup)
                        key.SetValue(RunKeyName, exePath);
                    else
                    {
                        try { key.DeleteValue(RunKeyName, false); } catch { /* value may not exist */ }
                    }
                }
            }
            catch { /* ignore */ }
        }

        /// <summary>Desktop shortcut is created by the MSI (so it is removed on uninstall). InstallerUI no longer creates one.</summary>
        public void CreateDesktopShortcut()
        {
            // No-op: MSI creates the desktop shortcut so uninstall removes it.
        }

        private static bool CreateShortcutAt(Type shellType, string shortcutPath, string exePath, string workingDir)
        {
            try
            {
                var shell = Activator.CreateInstance(shellType);
                var shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                if (shortcut == null) return false;
                var shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
                shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { "X-PHY Deepfake Detector" });
                shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
                return true;
            }
            catch { return false; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute ?? (() => true);
        }

        public bool CanExecute(object parameter) => _canExecute();
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
