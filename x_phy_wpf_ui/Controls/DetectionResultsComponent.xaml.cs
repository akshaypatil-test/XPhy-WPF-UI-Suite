using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        private DispatcherTimer _fakeProgressTimer;
        private DispatcherTimer _deepfakeNotificationTimer;
        private DateTime _detectionStartTime;
        private bool _hasReceivedFaceResults;
        private bool _deepfakeNotificationRaised;

        /// <summary>Last average fake percentage (0-100) when deepfake was detected. Used by deepfake notification.</summary>
        public int LastConfidencePercent { get; private set; }
        /// <summary>Latest evidence frame image for notification. Used by deepfake notification.</summary>
        public System.Windows.Media.Imaging.BitmapSource LatestEvidenceImage { get; private set; }

        public DetectionResultsComponent()
        {
            InitializeComponent();
            _detectedFaces = new ObservableCollection<FaceViewModel>();
            FacesItemsControl.ItemsSource = _detectedFaces;
        }

        private SolidColorBrush GetBrush(string key)
        {
            return (SolidColorBrush)Resources[key];
        }

        /// <summary>Call when detection starts. Resets UI and starts fake progress timer.</summary>
        public void StartDetection(bool isAudioMode)
        {
            _hasReceivedFaceResults = false;
            _deepfakeNotificationRaised = false;
            _detectedFaces.Clear();

            DuringDetectionPanel.Visibility = Visibility.Visible;
            FakeProgressPercentText.Text = "Analyzing... 0%";
            DuringDetectionSubtext.Text = "Detection in progress (60s)";
            try
            {
                DetectionLiveImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/face.png"));
            }
            catch { }

            StopButtonPanel.Visibility = Visibility.Visible;
            BackButtonPanel.Visibility = Visibility.Collapsed;
            StopButtonAlways.IsEnabled = true;

            ClassificationIndicator.Visibility = Visibility.Visible;
            ClassificationIcon.Text = "⏳";
            ClassificationText.Text = "ANALYZING";
            ClassificationText.Foreground = GetBrush("AnalyzingColor");
            ClassificationPercentage.Text = "";
            string type = isAudioMode ? "Audio" : "Video";
            ClassificationSubtext.Text = $"Starting {type} detection for 60 seconds...";

            TotalFacesText.Text = "0";
            FakeFacesText.Text = "0";
            AvgFakePercentText.Text = "0%";

            _detectionStartTime = DateTime.UtcNow;
            _fakeProgressTimer?.Stop();
            _fakeProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _fakeProgressTimer.Tick += FakeProgressTimer_Tick;
            _fakeProgressTimer.Start();
            StopDeepfakeNotificationTimer();
        }

        private void FakeProgressTimer_Tick(object sender, EventArgs e)
        {
            if (_hasReceivedFaceResults) return;
            var elapsed = (DateTime.UtcNow - _detectionStartTime).TotalSeconds;
            int percent = Math.Min(100, (int)(elapsed / 60.0 * 100.0));
            FakeProgressPercentText.Text = $"Analyzing... {percent}%";
            if (percent >= 100)
            {
                _fakeProgressTimer?.Stop();
                _fakeProgressTimer = null;
            }
        }

        /// <summary>Update UI from face callback. Dynamic percentage and image.</summary>
        public void UpdateDetectedFaces(DetectedFace[] faces, Func<string, SolidColorBrush> getResourceBrush)
        {
            if (faces == null || faces.Length == 0) return;

            _hasReceivedFaceResults = true;

            double avgFake = faces.Average(f => f.ProbFakeScore * 100);
            FakeProgressPercentText.Text = $"Live — Avg suspicious: {avgFake:F1}%";
            DuringDetectionSubtext.Text = $"{faces.Length} face(s) detected";

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
                    if (bitmap != null)
                    {
                        currentFrameBitmap = bitmap;
                        DetectionLiveImage.Source = bitmap;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DetectionResultsComponent image convert: {ex.Message}");
                }
            }

            _detectedFaces.Clear();
            int fakeCount = 0;
            double totalFakePercent = 0;

            foreach (var face in faces)
            {
                var vm = new FaceViewModel
                {
                    Image = null,
                    StatusText = face.IsFake ? "FAKE" : "REAL",
                    StatusColor = face.IsFake ? getResourceBrush("FakeColor") : getResourceBrush("RealColor"),
                    PercentageText = $"{(face.ProbFakeScore * 100):F1}%"
                };
                _detectedFaces.Add(vm);
                if (face.IsFake) fakeCount++;
                totalFakePercent += face.ProbFakeScore * 100;
            }

            TotalFacesText.Text = faces.Length.ToString();
            FakeFacesText.Text = fakeCount.ToString();
            AvgFakePercentText.Text = faces.Length > 0 ? $"{(totalFakePercent / faces.Length):F1}%" : "0%";

            if (fakeCount > 0)
            {
                double avgPct = faces.Length > 0 ? (totalFakePercent / faces.Length) : 0;
                LastConfidencePercent = (int)Math.Round(Math.Min(100, avgPct));
                if (currentFrameBitmap != null)
                    LatestEvidenceImage = currentFrameBitmap;
                ClassificationIcon.Text = "⚠️";
                ClassificationText.Text = "DEEPFAKE DETECTED";
                ClassificationText.Foreground = GetBrush("FakeColor");
                if (!_deepfakeNotificationRaised)
                {
                    _deepfakeNotificationRaised = true;
                    DeepfakeDetected?.Invoke(this, EventArgs.Empty);
                }
                StartDeepfakeNotificationTimer();
            }
            else
            {
                ClassificationIcon.Text = "✓";
                ClassificationText.Text = "REAL";
                ClassificationText.Foreground = GetBrush("RealColor");
                StopDeepfakeNotificationTimer();
            }

            double overallPct = faces.Length > 0 ? (totalFakePercent / faces.Length) : 0;
            ClassificationPercentage.Text = $"({overallPct:F1}% Suspicious)";
            ClassificationSubtext.Text = $"Detected {faces.Length} face(s): {fakeCount} fake, {faces.Length - fakeCount} real";
        }

        /// <summary>Update classification from video callback.</summary>
        public void UpdateOverallClassification(bool isDeepfake)
        {
            ClassificationIndicator.Visibility = Visibility.Visible;
            if (isDeepfake)
            {
                if (_detectedFaces.Count > 0)
                {
                    int fakeCount = _detectedFaces.Count(f => f.StatusText == "FAKE");
                    LastConfidencePercent = (int)Math.Round((double)fakeCount / _detectedFaces.Count * 100);
                }
                ClassificationIcon.Text = "⚠️";
                ClassificationText.Text = "DEEPFAKE DETECTED";
                ClassificationText.Foreground = GetBrush("FakeColor");
                if (!_deepfakeNotificationRaised)
                {
                    _deepfakeNotificationRaised = true;
                    DeepfakeDetected?.Invoke(this, EventArgs.Empty);
                }
                StartDeepfakeNotificationTimer();
            }
            else
            {
                ClassificationIcon.Text = "✓";
                ClassificationText.Text = "REAL";
                ClassificationText.Foreground = GetBrush("RealColor");
                StopDeepfakeNotificationTimer();
            }

            if (_detectedFaces.Count > 0)
            {
                int fakeCount = _detectedFaces.Count(f => f.StatusText == "FAKE");
                double pct = (double)fakeCount / _detectedFaces.Count * 100;
                ClassificationPercentage.Text = $"({pct:F1}% Suspicious)";
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
                    if (!_deepfakeNotificationRaised)
                    {
                        _deepfakeNotificationRaised = true;
                        DeepfakeDetected?.Invoke(this, EventArgs.Empty);
                    }
                    StartDeepfakeNotificationTimer();
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
            _fakeProgressTimer?.Stop();
            _fakeProgressTimer = null;
            StopDeepfakeNotificationTimer();
            DuringDetectionPanel.Visibility = Visibility.Collapsed;

            if (_detectedFaces.Count > 0)
            {
                int fakeCount = _detectedFaces.Count(f => f.StatusText == "FAKE");
                double pct = (double)fakeCount / _detectedFaces.Count * 100;
                ClassificationPercentage.Text = $"({pct:F1}% Suspicious)";
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
            _fakeProgressTimer?.Stop();
            _fakeProgressTimer = null;
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
