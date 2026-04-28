#nullable enable
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui
{
    /// <summary>
    /// Popup shown when multiple processes from our list are detected while the app is minimized.
    /// Prompts the user to open the app to choose one source for detection.
    /// </summary>
    public partial class MultipleSourcesDetectedPopup : Window
    {
        private static int _openCount;
        private bool _closeCounted;
        private DispatcherTimer? _autoDismissTimer;

        /// <summary>True if at least one multiple-sources popup is currently open. Used to avoid showing duplicates.</summary>
        public static bool IsAnyOpen => _openCount > 0;

        /// <summary>Set when the user chose Do Not Disturb (mute source alerts for this session) before the window closed.</summary>
        public bool ClosedWithDoNotDisturb { get; private set; }

        /// <summary>Fired when user clicks OPEN to restore the app and go to Select Detection Source.</summary>
        public event EventHandler? OpenApplicationRequested;

        public MultipleSourcesDetectedPopup()
        {
            InitializeComponent();
            VersionText.Text = "Version: " + ApplicationVersion.GetDisplayVersion();
            Loaded += (_, __) => StartAutoDismissTimer();
            Closed += (s, _) =>
            {
                StopAutoDismissTimer();
                if (_closeCounted) return;
                _closeCounted = true;
                if (_openCount > 0) _openCount--;
            };
        }

        private void StartAutoDismissTimer()
        {
            StopAutoDismissTimer();
            _autoDismissTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _autoDismissTimer.Tick += AutoDismissTimer_Tick;
            _autoDismissTimer.Start();
        }

        private void StopAutoDismissTimer()
        {
            if (_autoDismissTimer == null) return;
            _autoDismissTimer.Tick -= AutoDismissTimer_Tick;
            _autoDismissTimer.Stop();
            _autoDismissTimer = null;
        }

        private void AutoDismissTimer_Tick(object? sender, EventArgs e)
        {
            StopAutoDismissTimer();
            if (!IsVisible) return;
            Close();
        }

        /// <summary>
        /// Show the popup at bottom-right of the working area.
        /// </summary>
        public void ShowAtBottomRight()
        {
            _openCount++;
            Loaded += OnLoadedPosition;
            SizeChanged += OnSizeChangedPosition;
            Show();
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
            StopAutoDismissTimer();
            OpenApplicationRequested?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            StopAutoDismissTimer();
            Close();
        }

        private void DoNotDisturbButton_Click(object sender, RoutedEventArgs e)
        {
            StopAutoDismissTimer();
            ClosedWithDoNotDisturb = true;
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }
    }
}
