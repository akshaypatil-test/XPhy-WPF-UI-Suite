#nullable enable
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace x_phy_wpf_ui
{
    /// <summary>
    /// Popup shown when multiple processes from our list are detected while the app is minimized.
    /// Prompts the user to open the app to choose one source for detection.
    /// </summary>
    public partial class MultipleSourcesDetectedPopup : Window
    {
        /// <summary>Fired when user clicks "Open Application" to restore the minimized app.</summary>
        public event EventHandler? OpenApplicationRequested;

        public MultipleSourcesDetectedPopup()
        {
            InitializeComponent();
            VersionText.Text = "Version: " + GetAppVersion();
        }

        private static string GetAppVersion()
        {
            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.10";
            }
            catch
            {
                return "1.0.10";
            }
        }

        /// <summary>
        /// Show the popup at bottom-right of the working area.
        /// </summary>
        public void ShowAtBottomRight()
        {
            Loaded += OnLoadedPosition;
            SizeChanged += OnSizeChangedPosition;
            Show();
            Activate();
            Dispatcher.BeginInvoke(new Action(EnsureFitsScreen), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnLoadedPosition(object sender, RoutedEventArgs e)
        {
            EnsureFitsScreen();
        }

        private void OnSizeChangedPosition(object sender, SizeChangedEventArgs e)
        {
            EnsureFitsScreen();
        }

        private void EnsureFitsScreen()
        {
            if (!IsLoaded || !IsVisible) return;
            var workArea = SystemParameters.WorkArea;
            const int margin = 16;
            Left = workArea.Right - Width - margin;
            double desiredTop = workArea.Bottom - ActualHeight - margin;
            Top = Math.Max(workArea.Top + margin, desiredTop);
        }

        private void OpenApplication_Click(object sender, RoutedEventArgs e)
        {
            OpenApplicationRequested?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }
    }
}
