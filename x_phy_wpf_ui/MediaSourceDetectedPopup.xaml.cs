#nullable enable
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui
{
    /// <summary>
    /// Popup shown when exactly one process from our list is detected while the app is minimized.
    /// Lets the user start detection for that source (conference or web stream video/audio).
    /// </summary>
    public partial class MediaSourceDetectedPopup : Window
    {
        private DetectionSource _option1Source;
        private DetectionSource _option2Source;

        /// <summary>Fired when user clicks the first button (video). Args: DetectionSource, isLiveCallMode, isAudioMode.</summary>
        public event EventHandler<MediaSourceDetectedChoiceEventArgs>? StartDetectionChosen;

        public MediaSourceDetectedPopup()
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
        /// Configure the popup for the single detected process and show it at bottom-right.
        /// </summary>
        public void ShowForProcess(DetectedProcess process)
        {
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

        private void Option1_Click(object sender, RoutedEventArgs e)
        {
            bool isLiveCall = _option1Source == DetectionSource.ZoomConferenceVideo || _option1Source == DetectionSource.ZoomConferenceAudio;
            bool isAudio = _option1Source == DetectionSource.ZoomConferenceAudio ||
                          _option1Source == DetectionSource.VLCWebStreamAudio ||
                          _option1Source == DetectionSource.YouTubeWebStreamAudio;
            StartDetectionChosen?.Invoke(this, new MediaSourceDetectedChoiceEventArgs(_option1Source, isLiveCall, isAudio));
            Close();
        }

        private void Option2_Click(object sender, RoutedEventArgs e)
        {
            bool isLiveCall = _option2Source == DetectionSource.ZoomConferenceVideo || _option2Source == DetectionSource.ZoomConferenceAudio;
            bool isAudio = _option2Source == DetectionSource.ZoomConferenceAudio ||
                          _option2Source == DetectionSource.VLCWebStreamAudio ||
                          _option2Source == DetectionSource.YouTubeWebStreamAudio;
            StartDetectionChosen?.Invoke(this, new MediaSourceDetectedChoiceEventArgs(_option2Source, isLiveCall, isAudio));
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
