#nullable enable
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui
{
    /// <summary>
    /// Popup shown when exactly one process from our list is detected while the app is minimized.
    /// Lets the user start detection for that source (conference or web stream video/audio).
    /// </summary>
    public partial class MediaSourceDetectedPopup : Window
    {
        private static int _openCount;
        private bool _closeCounted;
        private readonly MouseButtonEventHandler _dndLightDismissPreviewHandler;
        private DispatcherTimer? _autoDismissTimer;
        private const int AutoDismissSeconds = 7;
        /// <summary>Avoid restarting the auto-dismiss timer when the DND list closes because the whole notification is closing.</summary>
        private bool _notificationClosing;

        /// <summary>True if at least one single-process (media source) popup is currently open. Used to avoid showing duplicates.</summary>
        public static bool IsAnyOpen => _openCount > 0;

        /// <summary>Set when the user chose DO NOT DISTURB Yes (snooze single-source alerts) before the window closed.</summary>
        public bool ClosedWithSnooze { get; private set; }

        private DetectionSource _option1Source;
        private DetectionSource _option2Source;

        /// <summary>Fired when user clicks the first button (video). Args: DetectionSource, isLiveCallMode, isAudioMode.</summary>
        public event EventHandler<MediaSourceDetectedChoiceEventArgs>? StartDetectionChosen;

        public MediaSourceDetectedPopup()
        {
            InitializeComponent();
            _dndLightDismissPreviewHandler = OnPreviewMouseCloseDndIfOutside;
            VersionText.Text = "Version: " + ApplicationVersion.GetDisplayVersion();
            DndListPopup.Opened += OnDndListPopupOpened;
            DndListPopup.Closed += OnDndListPopupClosed;
            Loaded += (_, __) => StartAutoDismissTimer();
            Closing += (_, __) => { _notificationClosing = true; };
            Closed += (s, _) =>
            {
                StopAutoDismissTimer();
                try { DndListPopup.IsOpen = false; } catch { /* designer / early close */ }
                try { RemoveHandler(PreviewMouseLeftButtonDownEvent, _dndLightDismissPreviewHandler); } catch { }
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
                Interval = TimeSpan.FromSeconds(AutoDismissSeconds)
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

        private void OnDndListPopupOpened(object sender, EventArgs e)
        {
            try { RemoveHandler(PreviewMouseLeftButtonDownEvent, _dndLightDismissPreviewHandler); } catch { /* not registered */ }
            AddHandler(PreviewMouseLeftButtonDownEvent, _dndLightDismissPreviewHandler, handledEventsToo: true);
            // Do not auto-dismiss while the user may be choosing Yes/No on Do Not Disturb.
            StopAutoDismissTimer();
        }

        private void OnDndListPopupClosed(object sender, EventArgs e)
        {
            try { RemoveHandler(PreviewMouseLeftButtonDownEvent, _dndLightDismissPreviewHandler); } catch { /* duplicate */ }
            if (_notificationClosing || !IsVisible) return;
            // Fresh auto-dismiss interval after the list closes (No, light-dismiss, or toggle closed) so the rest of the UI stays usable.
            StartAutoDismissTimer();
        }

        private static bool IsDescendantOf(DependencyObject? node, DependencyObject? potentialAncestor)
        {
            while (node != null)
            {
                if (ReferenceEquals(node, potentialAncestor)) return true;
                node = VisualTreeHelper.GetParent(node);
            }
            return false;
        }

        /// <summary>StaysOpen=True avoids the open-click immediately dismissing the list; we mimic light-dismiss here.</summary>
        private void OnPreviewMouseCloseDndIfOutside(object sender, MouseButtonEventArgs e)
        {
            if (!DndListPopup.IsOpen) return;
            if (e.OriginalSource is not DependencyObject src) return;
            if (IsDescendantOf(src, DndTriggerBorder)) return;
            if (DndListPopup.Child is DependencyObject popupRoot && IsDescendantOf(src, popupRoot)) return;
            DndListPopup.IsOpen = false;
        }

        /// <summary>
        /// Configure the popup for the single detected process and show it at bottom-right.
        /// </summary>
        public void ShowForProcess(DetectedProcess process)
        {
            DndSelectionText.Text = "Do Not Disturb";
            try { DndListPopup.IsOpen = false; } catch { }

            TitleText.Text = $"[{process.DisplayName}] Media Source Detected";

            if (process.ProcessType == "VideoCalling")
            {
                Option1Button.Content = "Start conference video";
                Option2Button.Content = "Start conference Audio";
                _option1Source = DetectionSource.ZoomConferenceVideo;
                _option2Source = DetectionSource.ZoomConferenceAudio;
            }
            else
            {
                Option1Button.Content = "Start Web Stream Video";
                Option2Button.Content = "Start Web Stream Audio";
                _option1Source = process.ProcessType == "Browser"
                    ? DetectionSource.YouTubeWebStreamVideo
                    : DetectionSource.VLCWebStreamVideo;
                _option2Source = process.ProcessType == "Browser"
                    ? DetectionSource.YouTubeWebStreamAudio
                    : DetectionSource.VLCWebStreamAudio;
            }

            Loaded += OnLoadedPosition;
            SizeChanged += OnSizeChangedPosition;
            _openCount++;
            Show();
            Activate();
            Dispatcher.BeginInvoke(new Action(EnsureFitsScreen), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void DndTriggerBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DndListPopup.IsOpen = !DndListPopup.IsOpen;
        }

        private void DndOptionYes_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DndListPopup.IsOpen = false;
            StopAutoDismissTimer();
            ClosedWithSnooze = true;
            Close();
        }

        private void DndOptionNo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DndListPopup.IsOpen = false;
            DndSelectionText.Text = "No";
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

        private void Option1_Click(object sender, RoutedEventArgs e)
        {
            StopAutoDismissTimer();
            bool isLiveCall = _option1Source == DetectionSource.ZoomConferenceVideo || _option1Source == DetectionSource.ZoomConferenceAudio;
            bool isAudio = _option1Source == DetectionSource.ZoomConferenceAudio ||
                          _option1Source == DetectionSource.VLCWebStreamAudio ||
                          _option1Source == DetectionSource.YouTubeWebStreamAudio;
            StartDetectionChosen?.Invoke(this, new MediaSourceDetectedChoiceEventArgs(_option1Source, isLiveCall, isAudio));
            Close();
        }

        private void Option2_Click(object sender, RoutedEventArgs e)
        {
            StopAutoDismissTimer();
            bool isLiveCall = _option2Source == DetectionSource.ZoomConferenceVideo || _option2Source == DetectionSource.ZoomConferenceAudio;
            bool isAudio = _option2Source == DetectionSource.ZoomConferenceAudio ||
                          _option2Source == DetectionSource.VLCWebStreamAudio ||
                          _option2Source == DetectionSource.YouTubeWebStreamAudio;
            StartDetectionChosen?.Invoke(this, new MediaSourceDetectedChoiceEventArgs(_option2Source, isLiveCall, isAudio));
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            StopAutoDismissTimer();
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }
    }

    public class MediaSourceDetectedChoiceEventArgs : EventArgs
    {
        public DetectionSource Source { get; }
        public bool IsLiveCallMode { get; }
        public bool IsAudioMode { get; }

        public MediaSourceDetectedChoiceEventArgs(DetectionSource source, bool isLiveCallMode, bool isAudioMode)
        {
            Source = source;
            IsLiveCallMode = isLiveCallMode;
            IsAudioMode = isAudioMode;
        }
    }
}
