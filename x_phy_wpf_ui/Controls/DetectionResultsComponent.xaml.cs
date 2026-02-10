using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using XPhyWrapper;
using x_phy_wpf_ui;

namespace x_phy_wpf_ui.Controls
{
    public partial class DetectionResultsComponent : UserControl
    {
        public event EventHandler StopDetectionClicked;
        public event EventHandler BackToHomeClicked;
        public event EventHandler DeepfakeDetected;

        private readonly ObservableCollection<FaceViewModel> _detectedFaces;
        private DispatcherTimer _deepfakeNotificationTimer;
        private DateTime _detectionStartTime;
        private bool _hasReceivedFaceResults;

        // Arc loader (same as LoaderComponent)
        private const double ArcCenterX = 60;
        private const double ArcCenterY = 60;
        private const double ArcRadius = 48;
        private const double ArcSweepDegrees = 100;
        private Storyboard _detectionArcStoryboard;
        public static readonly DependencyProperty DetectionArcOffsetAngleProperty = DependencyProperty.Register(
            nameof(DetectionArcOffsetAngle), typeof(double), typeof(DetectionResultsComponent),
            new PropertyMetadata(0.0, OnDetectionArcOffsetAngleChanged));

        private double DetectionArcOffsetAngle
        {
            get => (double)GetValue(DetectionArcOffsetAngleProperty);
            set => SetValue(DetectionArcOffsetAngleProperty, value);
        }

        // At most 4 notifications in 60s: windows 0-15s, 15-30s, 30-45s, 45-60s
        private const int NotificationWindowSeconds = 15;
        private const int NotificationWindowCount = 4;
        private int _lastNotificationWindowIndex = -1;
        private double _windowSumScore;
        private int _windowCount;
        private bool _windowHadFake;
        private System.Windows.Media.Imaging.BitmapSource _windowEvidenceImage;

        /// <summary>Last average fake percentage (0-100) when deepfake was detected. Used by deepfake notification.</summary>
        public int LastConfidencePercent { get; private set; }
        /// <summary>Latest evidence frame image for notification. Used by deepfake notification.</summary>
        public System.Windows.Media.Imaging.BitmapSource LatestEvidenceImage { get; private set; }

        public DetectionResultsComponent()
        {
            InitializeComponent();
            _detectedFaces = new ObservableCollection<FaceViewModel>();
            FacesItemsControl.ItemsSource = _detectedFaces;
            Loaded += (s, e) => { };
            Unloaded += (s, e) => StopDetectionArcAnimation();
        }

        private static void OnDetectionArcOffsetAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DetectionResultsComponent c)
                c.UpdateDetectionArcGeometry();
        }

        private void UpdateDetectionArcGeometry()
        {
            if (DetectionRollingArcPath == null) return;
            double angle = DetectionArcOffsetAngle * Math.PI / 180.0;
            double endAngle = (DetectionArcOffsetAngle + ArcSweepDegrees) * Math.PI / 180.0;
            double startX = ArcCenterX + ArcRadius * Math.Cos(angle);
            double startY = ArcCenterY + ArcRadius * Math.Sin(angle);
            double endX = ArcCenterX + ArcRadius * Math.Cos(endAngle);
            double endY = ArcCenterY + ArcRadius * Math.Sin(endAngle);
            var pathFigure = new PathFigure(
                new Point(startX, startY),
                new PathSegment[] { new ArcSegment(new Point(endX, endY), new Size(ArcRadius, ArcRadius), 0, ArcSweepDegrees > 180, SweepDirection.Clockwise, true) },
                false);
            DetectionRollingArcPath.Data = new PathGeometry(new[] { pathFigure });
        }

        private void StartDetectionArcAnimation()
        {
            StopDetectionArcAnimation();
            UpdateDetectionArcGeometry();
            _detectionArcStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var animation = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromMilliseconds(1200)));
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(DetectionArcOffsetAngleProperty));
            _detectionArcStoryboard.Children.Add(animation);
            _detectionArcStoryboard.Begin(this, true);
        }

        private void StopDetectionArcAnimation()
        {
            _detectionArcStoryboard?.Stop(this);
            _detectionArcStoryboard = null;
        }

        private SolidColorBrush GetBrush(string key)
        {
            return (SolidColorBrush)Resources[key];
        }

        /// <summary>Call when detection starts. Shows lock+arc loader only; hides classification/stats/face list.</summary>
        public void StartDetection(bool isAudioMode)
        {
            _hasReceivedFaceResults = false;
            _detectedFaces.Clear();
            _lastNotificationWindowIndex = -1;
            _windowSumScore = 0;
            _windowCount = 0;
            _windowHadFake = false;
            _windowEvidenceImage = null;

            DuringDetectionPanel.Visibility = Visibility.Visible;
            if (ClassificationIndicator != null) ClassificationIndicator.Visibility = Visibility.Collapsed;
            if (StatisticsCardsGrid != null) StatisticsCardsGrid.Visibility = Visibility.Collapsed;
            if (FaceListScrollViewer != null) FaceListScrollViewer.Visibility = Visibility.Collapsed;

            StopButtonPanel.Visibility = Visibility.Visible;
            BackButtonPanel.Visibility = Visibility.Collapsed;
            StopButtonAlways.IsEnabled = true;

            TotalFacesText.Text = "0";
            FakeFacesText.Text = "0";
            AvgFakePercentText.Text = "0%";

            _detectionStartTime = DateTime.UtcNow;
            StopDeepfakeNotificationTimer();
            StartDetectionArcAnimation();
        }

        /// <summary>Update from face callback. At most 4 notifications in 60s (windows 0-15, 15-30, 30-45, 45-60). Each notification uses average ProbFakeScore*100 and the evidence image from that window.</summary>
        public void UpdateDetectedFaces(DetectedFace[] faces, Func<string, SolidColorBrush> getResourceBrush)
        {
            if (faces == null || faces.Length == 0) return;

            _hasReceivedFaceResults = true;
            var elapsed = (DateTime.UtcNow - _detectionStartTime).TotalSeconds;
            int currentWindow = Math.Min(NotificationWindowCount - 1, (int)(elapsed / NotificationWindowSeconds));

            // Evidence image for this batch (same instance as this face data)
            DetectedFace? latestWithImage = null;
            for (int i = faces.Length - 1; i >= 0; i--)
            {
                if (faces[i].ImageData != null && faces[i].ImageData.Length > 0)
                {
                    latestWithImage = faces[i];
                    break;
                }
            }
            System.Windows.Media.Imaging.BitmapSource currentFrameBitmap = null;
            if (latestWithImage.HasValue)
            {
                try
                {
                    var bitmap = ImageHelper.ConvertToBitmapSource(latestWithImage.Value);
                    if (bitmap != null) currentFrameBitmap = bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DetectionResultsComponent image convert: {ex.Message}");
                }
            }

            // When crossing into a new window: flush previous window and show at most one notification with that window's confidence and image
            if (currentWindow > _lastNotificationWindowIndex && _lastNotificationWindowIndex >= 0)
            {
                if (_windowHadFake && _windowCount > 0)
                {
                    double avgScore = _windowSumScore / _windowCount;
                    LastConfidencePercent = (int)Math.Round(Math.Min(100, Math.Max(0, avgScore * 100)));
                    LatestEvidenceImage = _windowEvidenceImage;
                    DeepfakeDetected?.Invoke(this, EventArgs.Empty);
                }
                _windowSumScore = 0;
                _windowCount = 0;
                _windowHadFake = false;
                _windowEvidenceImage = null;
            }
            _lastNotificationWindowIndex = currentWindow;

            // Accumulate this batch into current window (confidence = average ProbFakeScore; image = from this instance)
            double batchSum = 0;
            int fakeCount = 0;
            foreach (var face in faces)
            {
                batchSum += face.ProbFakeScore;
                if (face.IsFake) fakeCount++;
            }
            _windowSumScore += batchSum;
            _windowCount += faces.Length;
            if (fakeCount > 0) _windowHadFake = true;
            if (currentFrameBitmap != null) _windowEvidenceImage = currentFrameBitmap;

            // Keep _detectedFaces for final result when detection completes
            _detectedFaces.Clear();
            double totalFakePercent = 0;
            foreach (var face in faces)
            {
                double confidencePercent = face.ProbFakeScore * 100;
                _detectedFaces.Add(new FaceViewModel
                {
                    Image = null,
                    StatusText = face.IsFake ? "FAKE" : "REAL",
                    StatusColor = face.IsFake ? getResourceBrush("FakeColor") : getResourceBrush("RealColor"),
                    PercentageText = $"{confidencePercent:F1}%",
                    ConfidencePercent = confidencePercent
                });
                totalFakePercent += face.ProbFakeScore * 100;
            }
            TotalFacesText.Text = faces.Length.ToString();
            FakeFacesText.Text = fakeCount.ToString();
            AvgFakePercentText.Text = faces.Length > 0 ? $"{(totalFakePercent / faces.Length):F1}%" : "0%";
        }

        /// <summary>Update classification from video callback. Notifications are only sent from UpdateDetectedFaces (4 windows in 60s).</summary>
        public void UpdateOverallClassification(bool isDeepfake)
        {
            if (ClassificationIndicator != null) ClassificationIndicator.Visibility = Visibility.Visible;
            if (isDeepfake)
            {
                if (_detectedFaces.Count > 0)
                {
                    double avgConfidence = _detectedFaces.Average(f => f.ConfidencePercent);
                    LastConfidencePercent = (int)Math.Round(Math.Min(100, Math.Max(0, avgConfidence)));
                }
                ClassificationIcon.Text = "⚠️";
                ClassificationText.Text = "DEEPFAKE DETECTED";
                ClassificationText.Foreground = GetBrush("FakeColor");
            }
            else
            {
                ClassificationIcon.Text = "✓";
                ClassificationText.Text = "REAL";
                ClassificationText.Foreground = GetBrush("RealColor");
            }

            if (_detectedFaces.Count > 0)
            {
                int fakeCount = _detectedFaces.Count(f => f.StatusText == "FAKE");
                double avgConfidence = _detectedFaces.Average(f => f.ConfidencePercent);
                ClassificationPercentage.Text = $"({avgConfidence:F1}% Suspicious)";
                ClassificationSubtext.Text = $"Detected {_detectedFaces.Count} face(s): {fakeCount} fake, {_detectedFaces.Count - fakeCount} real";
            }
            else
            {
                ClassificationPercentage.Text = "";
                ClassificationSubtext.Text = "Analyzing video...";
            }
        }

        /// <summary>Update classification from audio callback (0=Real, 1=Deepfake, 2=Analyzing, 3=Invalid, 4=None).</summary>
        public void UpdateAudioClassification(int classification)
        {
            ClassificationIndicator.Visibility = Visibility.Visible;
            switch (classification)
            {
                case 0:
                    ClassificationIcon.Text = "✓";
                    ClassificationText.Text = "REAL";
                    ClassificationText.Foreground = GetBrush("RealColor");
                    ClassificationPercentage.Text = "";
                    ClassificationSubtext.Text = "Audio classified as real";
                    StopDeepfakeNotificationTimer();
                    break;
                case 1:
                    LastConfidencePercent = 0; // Audio has no percentage
                    ClassificationIcon.Text = "⚠️";
                    ClassificationText.Text = "DEEPFAKE DETECTED";
                    ClassificationText.Foreground = GetBrush("FakeColor");
                    ClassificationPercentage.Text = "";
                    ClassificationSubtext.Text = "Audio classified as deepfake";
                    DeepfakeDetected?.Invoke(this, EventArgs.Empty);
                    break;
                case 2:
                    ClassificationIcon.Text = "⏳";
                    ClassificationText.Text = "ANALYZING";
                    ClassificationText.Foreground = GetBrush("AnalyzingColor");
                    ClassificationPercentage.Text = "";
                    ClassificationSubtext.Text = "Analyzing audio...";
                    StopDeepfakeNotificationTimer();
                    break;
                case 3:
                    ClassificationIcon.Text = "❌";
                    ClassificationText.Text = "INVALID";
                    ClassificationText.Foreground = GetBrush("SecondaryTextColor");
                    ClassificationPercentage.Text = "";
                    ClassificationSubtext.Text = "Invalid audio sample";
                    StopDeepfakeNotificationTimer();
                    break;
                default:
                    ClassificationIcon.Text = "⏳";
                    ClassificationText.Text = "ANALYZING";
                    ClassificationText.Foreground = GetBrush("AnalyzingColor");
                    ClassificationPercentage.Text = "";
                    ClassificationSubtext.Text = "Waiting for audio...";
                    StopDeepfakeNotificationTimer();
                    break;
            }
        }

        /// <summary>Flush the last 15s window so the 4th notification (45-60s) can be shown if that window had fake.</summary>
        private void FlushLastNotificationWindow()
        {
            if (_windowHadFake && _windowCount > 0)
            {
                double avgScore = _windowSumScore / _windowCount;
                LastConfidencePercent = (int)Math.Round(Math.Min(100, Math.Max(0, avgScore * 100)));
                LatestEvidenceImage = _windowEvidenceImage;
                DeepfakeDetected?.Invoke(this, EventArgs.Empty);
            }
        }

        private void StartDeepfakeNotificationTimer()
        {
            // Only start if not already running - otherwise we'd reset the 10-sec countdown on every face update
            if (_deepfakeNotificationTimer != null)
                return;
            _deepfakeNotificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _deepfakeNotificationTimer.Tick += (s, e) =>
            {
                DeepfakeDetected?.Invoke(this, EventArgs.Empty);
                // Keep timer running so it fires again in 10 sec (no need to restart, DispatcherTimer repeats)
            };
            _deepfakeNotificationTimer.Start();
        }

        private void StopDeepfakeNotificationTimer()
        {
            _deepfakeNotificationTimer?.Stop();
            _deepfakeNotificationTimer = null;
        }

        /// <summary>Show final result and Back to Home button after 60s.</summary>
        public void ShowFinalResult(string resultPath)
        {
            StopDetectionArcAnimation();
            StopDeepfakeNotificationTimer();
            FlushLastNotificationWindow();
            DuringDetectionPanel.Visibility = Visibility.Collapsed;

            if (_detectedFaces.Count > 0)
            {
                int fakeCount = _detectedFaces.Count(f => f.StatusText == "FAKE");
                double avgConfidence = _detectedFaces.Average(f => f.ConfidencePercent);
                ClassificationPercentage.Text = $"({avgConfidence:F1}% Suspicious)";
                if (fakeCount > 0)
                {
                    ClassificationIcon.Text = "⚠️";
                    ClassificationText.Text = "DEEPFAKE DETECTED";
                    ClassificationText.Foreground = GetBrush("FakeColor");
                }
                else
                {
                    ClassificationIcon.Text = "✓";
                    ClassificationText.Text = "REAL";
                    ClassificationText.Foreground = GetBrush("RealColor");
                }
                ClassificationSubtext.Text = !string.IsNullOrEmpty(resultPath)
                    ? $"Detection completed. Results saved to: {resultPath}"
                    : "Detection completed. Results saved.";
            }
            else
            {
                ClassificationIcon.Text = "✓";
                ClassificationText.Text = "REAL";
                ClassificationText.Foreground = GetBrush("RealColor");
                ClassificationPercentage.Text = "(0.0% Suspicious)";
                ClassificationSubtext.Text = string.IsNullOrEmpty(resultPath) ? "Detection completed. Results saved." : $"Detection completed. Results saved to: {resultPath}";
            }

            StopButtonPanel.Visibility = Visibility.Collapsed;
            BackButtonPanel.Visibility = Visibility.Visible;
            StopButtonAlways.IsEnabled = false;
        }

        /// <summary>Reset and hide during-detection panel; call when stopping early.</summary>
        public void Reset()
        {
            StopDetectionArcAnimation();
            StopDeepfakeNotificationTimer();
            DuringDetectionPanel.Visibility = Visibility.Collapsed;
            _detectedFaces.Clear();
            TotalFacesText.Text = "0";
            FakeFacesText.Text = "0";
            AvgFakePercentText.Text = "0%";
            ClassificationIcon.Text = "";
            ClassificationText.Text = "";
            ClassificationPercentage.Text = "";
            ClassificationSubtext.Text = "";
            StopButtonPanel.Visibility = Visibility.Collapsed;
            BackButtonPanel.Visibility = Visibility.Collapsed;
        }

        public void SetStopButtonEnabled(bool enabled)
        {
            if (StopButtonAlways != null)
                StopButtonAlways.IsEnabled = enabled;
        }

        public int DetectedFacesCount => _detectedFaces?.Count ?? 0;

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopDetectionClicked?.Invoke(this, EventArgs.Empty);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackToHomeClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
