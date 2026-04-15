using System;
using System.Windows;
using System.Windows.Controls;
using InstallerUI.Views;

namespace InstallerUI
{
    /// <summary>
    /// Shell window for the WPF installer. The user only sees this UI; the MSI runs silently.
    /// We switch the content area (PageContent) based on InstallerViewModel.CurrentStep.
    /// MSI dialogs are not used—this WPF app controls the entire flow and runs msiexec /quiet when ready.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly InstallerViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new InstallerViewModel();
            DataContext = _vm;

            _vm.OnNavigate = step =>
            {
                UserControl view = step switch
                {
                    InstallerStep.Welcome => new WelcomeView(),
                    InstallerStep.LicenseAgreement => new LicenseView(),
                    InstallerStep.InstallPath => new InstallPathView(),
                    InstallerStep.Progress => new ProgressView(),
                    InstallerStep.Finish => new FinishView(),
                    _ => new WelcomeView()
                };
                view.DataContext = _vm;
                PageContent.Content = view;
            };

            _vm.OnRequestClose = () =>
            {
                try { Close(); }
                catch { }
                try { Application.Current?.Shutdown(0); }
                catch { }
            };

            // Finish button: launch app if "Launch now" checkbox is checked, then close (single handler for end screen).
            // We read the checkbox from the view so we use the actual UI state (binding may not have committed yet on click).
            FinishButton.Click += (s, e) =>
            {
                if (_vm.CurrentStep != InstallerStep.Finish)
                    return;

                // Use checkbox state from view so we don't rely on binding having committed yet on click
                bool launchNow = _vm.LaunchNow;
                if (PageContent.Content is FinishView finishView && finishView.LaunchNowCheckBox != null)
                {
                    if (finishView.LaunchNowCheckBox.IsChecked == true) launchNow = true;
                    else if (finishView.LaunchNowCheckBox.IsChecked == false) launchNow = false;
                }

                if (_vm.InstallSucceeded && launchNow)
                {
                    var (installDir, exePath) = InstallerViewModel.TryResolveInstalledExePath(_vm.InstallPath);
                    if (installDir != null && exePath != null && System.IO.File.Exists(exePath))
                    {
                        if (TryLaunchApp(exePath, installDir))
                        {
                            // Delay close so the launched process can start before installer exits
                            var timer = new System.Windows.Threading.DispatcherTimer
                            {
                                Interval = TimeSpan.FromMilliseconds(500)
                            };
                            timer.Tick += (_, __) =>
                            {
                                timer.Stop();
                                _vm.OnRequestClose?.Invoke();
                            };
                            timer.Start();
                            return;
                        }
                    }
                }
                _vm.OnRequestClose?.Invoke();
            };

            ShowInitialStep();

            Loaded += (s, e) =>
            {
                if (_vm.CurrentStep == InstallerStep.Finish)
                    return;
                var scenario = InstallerVersionCheck.EvaluateScenario();
                if (scenario == InstallVersionScenario.SameVersionInstalled ||
                    scenario == InstallVersionScenario.NewerVersionInstalled)
                    ShowInitialStep();
            };
        }

        /// <summary>Welcome for fresh/upgrade/unknown; Finish with message for same version or downgrade blocked.</summary>
        private void ShowInitialStep()
        {
            var scenario = InstallerVersionCheck.EvaluateScenario();
            if (scenario == InstallVersionScenario.SameVersionInstalled)
            {
                _vm.FailureMessage = InstallerVersionCheck.GetSameVersionMessage();
                _vm.InstallSucceeded = false;
                _vm.CurrentStep = InstallerStep.Finish;
                _vm.OnNavigate(InstallerStep.Finish);
            }
            else if (scenario == InstallVersionScenario.NewerVersionInstalled)
            {
                _vm.FailureMessage = InstallerVersionCheck.GetDowngradeBlockedMessage();
                _vm.InstallSucceeded = false;
                _vm.CurrentStep = InstallerStep.Finish;
                _vm.OnNavigate(InstallerStep.Finish);
            }
            else
            {
                _vm.OnNavigate(InstallerStep.Welcome);
            }
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        private void PrintLink_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (PageContent?.Content is LicenseView licenseView)
                licenseView.PrintLicense();
        }

        /// <summary>Launch the installed app. Uses explorer.exe so it runs in the user's session when installer is elevated (Admin).</summary>
        private static bool TryLaunchApp(string exePath, string workingDirectory)
        {
            try
            {
                // When installer runs as Admin, Process.Start(exe) can start the app in a different session (not visible).
                // Using explorer.exe to open the exe runs it in the interactive user's session.
                var startInfo = new System.Diagnostics.ProcessStartInfo("explorer.exe")
                {
                    UseShellExecute = true,
                    Arguments = "\"" + exePath + "\"",
                    WorkingDirectory = workingDirectory
                };
                System.Diagnostics.Process.Start(startInfo);
                return true;
            }
            catch
            {
                try
                {
                    // Fallback: direct start (may not show window if installer is elevated)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = workingDirectory
                    });
                    return true;
                }
                catch { return false; }
            }
        }
    }
}
