using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace x_phy_wpf_ui
{
    public partial class DetectionNotificationWindow : Window
    {
        private DispatcherTimer _autoCloseTimer;
        private string _resultPath;
        private Action _openResultsFolder;
        /// <summary>When set, "View Result" button navigates to the Results page instead of opening folder.</summary>
        private Action _navigateToResultsPage;
        /// <summary>When set, "Stop & View Results" calls this (stop detection, then open results when saved) instead of only opening folder.</summary>
        private Action _stopDetectionAndOpenResults;

        public DetectionNotificationWindow()
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

        /// <summary>Set content for simple notifications (Detection Started only).</summary>
        public void SetContent(string title, string message)
        {
            SimpleContentPanel.Visibility = Visibility.Visible;
            DeepfakeContentPanel.Visibility = Visibility.Collapsed;
            CompletedContentPanel.Visibility = Visibility.Collapsed;
            CompletedWithThreatContentPanel.Visibility = Visibility.Collapsed;
            TitleText.Text = title ?? "";
            MessageText.Text = message ?? "";
        }

        /// <summary>Set content for Detection Completed: message and View Result action.</summary>
        /// <param name="navigateToResultsPage">If set, "View Result" navigates to the Results page; otherwise uses openResultsFolder.</param>
        public void SetDetectionCompletedContent(string message, string resultPath, Action openResultsFolder, Action navigateToResultsPage = null)
        {
            SimpleContentPanel.Visibility = Visibility.Collapsed;
            DeepfakeContentPanel.Visibility = Visibility.Collapsed;
            CompletedWithThreatContentPanel.Visibility = Visibility.Collapsed;
            CompletedContentPanel.Visibility = Visibility.Visible;

            CompletedTitleText.Text = "Detection Completed";
            CompletedMessageText.Text = message ?? "No AI Manipulation Found";
            _resultPath = resultPath ?? "";
            _openResultsFolder = openResultsFolder;
            _navigateToResultsPage = navigateToResultsPage;
        }

        /// <summary>Set content for Detection Completed when AI manipulated content was detected: title, red alert, confidence, timestamp, evidence, Close + View Result.</summary>
        /// <param name="navigateToResultsPage">If set, "View Result" navigates to the Results page; otherwise uses openResultsFolder.</param>
        public void SetDetectionCompletedWithThreatContent(int confidencePercent, string resultPath, Action openResultsFolder,
            ImageSource evidenceImageLeft = null, ImageSource evidenceImageRight = null, Action navigateToResultsPage = null)
        {
            SimpleContentPanel.Visibility = Visibility.Collapsed;
            DeepfakeContentPanel.Visibility = Visibility.Collapsed;
            CompletedContentPanel.Visibility = Visibility.Collapsed;
            CompletedWithThreatContentPanel.Visibility = Visibility.Visible;

            CompletedThreatAlertText.Text = "AI Manipulated Content Detected";
            CompletedThreatConfidenceText.Text = $"Confidence {confidencePercent}%";
            CompletedThreatTimestampText.Text = DateTime.Now.ToString("HH:mm MMM d, yyyy");
            _resultPath = resultPath ?? "";
            _openResultsFolder = openResultsFolder;
            _navigateToResultsPage = navigateToResultsPage;

            CompletedThreatEvidenceLeft.Source = evidenceImageLeft;
            CompletedThreatEvidenceRight.Source = evidenceImageRight ?? evidenceImageLeft;
        }

        /// <summary>Set content for deepfake alert: confidence, result path, evidence images. Stop &amp; View Results stops detection, saves results, then opens folder.</summary>
        /// <param name="openResultsFolder">Opens the results folder (used when no stop-and-save flow).</param>
        /// <param name="stopDetectionAndOpenResults">If set, "Stop & View Results" calls this to stop detection and open results when saved; otherwise uses openResultsFolder.</param>
        public void SetDeepfakeContent(int confidencePercent, string resultPath, Action openResultsFolder,
            Action stopDetectionAndOpenResults = null,
            ImageSource evidenceImageLeft = null, ImageSource evidenceImageRight = null)
        {
            SimpleContentPanel.Visibility = Visibility.Collapsed;
            CompletedContentPanel.Visibility = Visibility.Collapsed;
            CompletedWithThreatContentPanel.Visibility = Visibility.Collapsed;
            DeepfakeContentPanel.Visibility = Visibility.Visible;
            WarningSection.Visibility = Visibility.Collapsed;

            AlertTitleText.Text = "AI Manipulated Content Detected";
            ConfidenceText.Text = $"Confidence {confidencePercent}%";
            TimestampText.Text = DateTime.Now.ToString("HH:mm MMM d, yyyy");

            _resultPath = resultPath ?? "";
            _openResultsFolder = openResultsFolder;
            _stopDetectionAndOpenResults = stopDetectionAndOpenResults;

            EvidenceImageLeft.Source = evidenceImageLeft;
            EvidenceImageRight.Source = evidenceImageRight ?? evidenceImageLeft;
        }

        public void ShowAtBottomRight(int autoCloseSeconds = 5)
        {
            Loaded += (s, e) => EnsureFitsScreen();
            SizeChanged += (s, e) => EnsureFitsScreen();
            Show();
            Activate();
            Dispatcher.BeginInvoke(new Action(EnsureFitsScreen), DispatcherPriority.Loaded);

            _autoCloseTimer?.Stop();
            if (autoCloseSeconds > 0)
            {
                _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(autoCloseSeconds) };
                _autoCloseTimer.Tick += (s, e) =>
                {
                    _autoCloseTimer.Stop();
                    _autoCloseTimer = null;
                    Close();
                };
                _autoCloseTimer.Start();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer?.Stop();
            _autoCloseTimer = null;
            Close();
        }

        private void StopAndViewResults_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer?.Stop();
            _autoCloseTimer = null;
            try
            {
                if (_stopDetectionAndOpenResults != null)
                {
                    _stopDetectionAndOpenResults.Invoke();
                }
                else
                {
                    _openResultsFolder?.Invoke();
                    string pathMessage = string.IsNullOrEmpty(_resultPath)
                        ? "Results folder opened."
                        : "Results folder opened.\n\nPath: " + _resultPath;
                    MessageBox.Show(pathMessage, "Results", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            Close();
        }

        private void CriticalThreatButton_Click(object sender, RoutedEventArgs e)
        {
            WarningSection.Visibility = WarningSection.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            Dispatcher.BeginInvoke(new Action(EnsureFitsScreen), DispatcherPriority.Loaded);
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

        private void CompletedClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CompletedViewResult_Click(object sender, RoutedEventArgs e)
        {
            _autoCloseTimer?.Stop();
            _autoCloseTimer = null;
            try
            {
                if (_navigateToResultsPage != null)
                {
                    _navigateToResultsPage.Invoke();
                    Close();
                }
                else
                {
                    _openResultsFolder?.Invoke();
                    if (!string.IsNullOrEmpty(_resultPath))
                    {
                        MessageBox.Show("Results folder opened.\n\nPath: " + _resultPath, "Results", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }
    }
}
