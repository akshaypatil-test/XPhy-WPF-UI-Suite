using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Controls
{
    public partial class SessionDetailsPanel : UserControl
    {
        /// <summary>Raised when the user clicks "Back to Results".</summary>
        public event EventHandler BackToResultsRequested;

        private readonly List<BitmapSource> _carouselImages = new List<BitmapSource>();
        private int _carouselIndex;

        public SessionDetailsPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Displays the given result and optionally loads media evidence from the results directory.
        /// </summary>
        public void SetResult(DetectionResultItem result, string resultsDirectory = null)
        {
            if (result == null) return;

            DetailConfidencePercent.Text = result.ConfidencePercent + "%";
            DetailConfidencePercent.Foreground = result.IsAiManipulationDetected
                ? (Brush)Resources["ResultRedBrush"]
                : (Brush)Resources["ResultGreenBrush"];

            DetailOutcomeText.Text = result.ResultText;
            DetailOutcomePill.Background = (Brush)Resources["OutcomePillBg"];
            if (result.IsAiManipulationDetected)
            {
                DetailOutcomePill.BorderBrush = (Brush)Resources["ResultRedBrush"];
                DetailOutcomeText.Foreground = (Brush)Resources["ResultRedBrush"];
            }
            else
            {
                DetailOutcomePill.BorderBrush = (Brush)Resources["ResultGreenBrush"];
                DetailOutcomeText.Foreground = (Brush)Resources["ResultGreenBrush"];
            }
            DetailOutcomePill.Effect = null;

            DetailDateAndTime.Text = result.Timestamp.ToString("HH:mm MMM d, yyyy");
            // Show application name (Zoom, Google Chrome, etc.) when available; otherwise "Local"
            DetailMediaSource.Text = !string.IsNullOrEmpty(result.MediaSourceDisplay)
                ? result.MediaSourceDisplay
                : "Local";
            DetailDetectionType.Text = result.DetectionTypeDisplay;
            DetailDuration.Text = result.DurationDisplay;

            _carouselImages.Clear();
            _carouselIndex = 0;
            CarouselImageLeft.Source = null;
            CarouselImageRight.Source = null;
            CarouselPrevButton.Visibility = Visibility.Collapsed;
            CarouselNextButton.Visibility = Visibility.Collapsed;
            CarouselCounterText.Visibility = Visibility.Collapsed;

            if (result.IsAiManipulationDetected)
            {
                MediaEvidenceNoManipulationPanel.Visibility = Visibility.Collapsed;
                MediaEvidenceImagesGrid.Visibility = Visibility.Visible;
                TryLoadMediaEvidence(result, resultsDirectory);
            }
            else
            {
                MediaEvidenceNoManipulationPanel.Visibility = Visibility.Visible;
                MediaEvidenceImagesGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void TryLoadMediaEvidence(DetectionResultItem result, string resultsDirectory)
        {
            string folderToUse = null;
            string singleFile = null;

            if (!string.IsNullOrEmpty(result.ResultPathOrId) && result.ResultPathOrId != "Local")
            {
                if (Directory.Exists(result.ResultPathOrId))
                    folderToUse = result.ResultPathOrId;
                else if (File.Exists(result.ResultPathOrId))
                    singleFile = result.ResultPathOrId;
            }

            if (folderToUse == null && singleFile == null && !string.IsNullOrEmpty(resultsDirectory))
            {
                var typeFolder = result.Type.Equals("Audio", StringComparison.OrdinalIgnoreCase) ? "audio" : "video";
                var dateFolder = result.Timestamp.ToString("dd-MM-yyyy");
                var timeFolder = result.Timestamp.ToString("HH-mm");
                var builtPath = Path.Combine(resultsDirectory, typeFolder, dateFolder, timeFolder);
                if (Directory.Exists(builtPath))
                    folderToUse = builtPath;
            }

            if (!string.IsNullOrEmpty(singleFile))
            {
                var bitmap = LoadBitmapFromPath(singleFile);
                if (bitmap != null)
                {
                    _carouselImages.Add(bitmap);
                    UpdateCarouselDisplay();
                }
                return;
            }

            if (string.IsNullOrEmpty(folderToUse)) return;
            try
            {
                var dir = new DirectoryInfo(folderToUse);
                var files = dir.GetFiles("*.png")
                    .Cast<FileInfo>()
                    .Concat(dir.GetFiles("*.jpg"))
                    .Concat(dir.GetFiles("*.jpeg"))
                    .OrderBy(f => f.Name)
                    .ToList();
                foreach (var fi in files)
                {
                    var bitmap = LoadBitmapFromPath(fi.FullName);
                    if (bitmap != null)
                        _carouselImages.Add(bitmap);
                }
                UpdateCarouselDisplay();
            }
            catch { /* ignore */ }
        }

        private static BitmapSource LoadBitmapFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                var uri = new Uri(path, UriKind.Absolute);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                try
                {
                    var fileUri = new Uri("file:///" + path.Replace("\\", "/").TrimStart('/'));
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = fileUri;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch { return null; }
            }
        }

        /// <summary>Number of "slides" (each slide shows 2 images: left + right).</summary>
        private int CarouselSlideCount => (_carouselImages.Count + 1) / 2;

        private void UpdateCarouselDisplay()
        {
            if (_carouselImages.Count == 0)
            {
                CarouselImageLeft.Source = null;
                CarouselImageRight.Source = null;
                CarouselPrevButton.Visibility = Visibility.Collapsed;
                CarouselNextButton.Visibility = Visibility.Collapsed;
                CarouselCounterText.Visibility = Visibility.Collapsed;
                return;
            }
            var slideCount = CarouselSlideCount;
            _carouselIndex = Math.Max(0, Math.Min(_carouselIndex, slideCount - 1));
            var leftIdx = 2 * _carouselIndex;
            var rightIdx = 2 * _carouselIndex + 1;
            CarouselImageLeft.Source = leftIdx < _carouselImages.Count ? _carouselImages[leftIdx] : null;
            CarouselImageRight.Source = rightIdx < _carouselImages.Count ? _carouselImages[rightIdx] : null;
            var showNav = slideCount > 1;
            CarouselPrevButton.Visibility = showNav ? Visibility.Visible : Visibility.Collapsed;
            CarouselNextButton.Visibility = showNav ? Visibility.Visible : Visibility.Collapsed;
            CarouselCounterText.Visibility = showNav ? Visibility.Visible : Visibility.Collapsed;
            CarouselCounterText.Text = $"{_carouselIndex + 1} of {slideCount}";
        }

        private void CarouselPrev_Click(object sender, RoutedEventArgs e)
        {
            if (CarouselSlideCount <= 1) return;
            _carouselIndex = _carouselIndex <= 0 ? CarouselSlideCount - 1 : _carouselIndex - 1;
            UpdateCarouselDisplay();
        }

        private void CarouselNext_Click(object sender, RoutedEventArgs e)
        {
            if (CarouselSlideCount <= 1) return;
            _carouselIndex = _carouselIndex >= CarouselSlideCount - 1 ? 0 : _carouselIndex + 1;
            UpdateCarouselDisplay();
        }

        private void BackToResults_Click(object sender, RoutedEventArgs e)
        {
            BackToResultsRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
