using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class DetectionResultsScreen : UserControl
    {
        private string _resultsDirectory;
        private ObservableCollection<DetectionResultItem> _items;
        private readonly SessionDetailsPanel _sessionDetailsPanel;

        public DetectionResultsScreen()
        {
            InitializeComponent();
            _sessionDetailsPanel = new SessionDetailsPanel();
            SessionDetailPanelHost.Content = _sessionDetailsPanel;
            _sessionDetailsPanel.BackToResultsRequested += (s, _) =>
            {
                ResultsListView.Visibility = Visibility.Visible;
                SessionDetailView.Visibility = Visibility.Collapsed;
            };
            _items = new ObservableCollection<DetectionResultItem>();
            ResultsDataGrid.ItemsSource = _items;
            ResultsDataGrid.LoadingRow += ResultsDataGrid_LoadingRow;
        }

        private void ResultsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Hide the new-item placeholder row and any null rows so no blank rows appear
            if (e.Row.Item == null || e.Row.Item == CollectionView.NewItemPlaceholder)
            {
                e.Row.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Sets the results directory path (from controller.GetResultsDir() or fallback) and reloads the list from local DB.
        /// Catches exceptions (e.g. SQLite native DLL missing when run from installer) so the app does not crash.
        /// </summary>
        public void SetResultsDirectoryAndRefresh(string resultsDir)
        {
            try
            {
                _resultsDirectory = resultsDir ?? DetectionResultsLoader.GetDefaultResultsDir();
                RefreshResults();
            }
            catch (Exception ex)
            {
                _items.Clear();
                System.Diagnostics.Debug.WriteLine($"DetectionResultsScreen.SetResultsDirectoryAndRefresh: {ex.Message}");
            }
        }

        /// <summary>
        /// Populates the grid from the backend GetResults API response. Use when API is available.
        /// </summary>
        public void SetResultsFromApi(System.Collections.Generic.IEnumerable<ResultDto> results)
        {
            _items.Clear();
            if (results == null) return;
            foreach (var r in results)
            {
                bool isAiDetected = (r.Outcome ?? "").Trim().Equals("AI Manipulation Detected", StringComparison.OrdinalIgnoreCase);
                _items.Add(new DetectionResultItem
                {
                    Timestamp = r.Timestamp,
                    Type = r.Type ?? "Video",
                    IsAiManipulationDetected = isAiDetected,
                    ConfidencePercent = (int)Math.Min(100, Math.Max(0, r.DetectionConfidence)),
                    ResultPathOrId = r.ArtifactPath ?? "", // Path for loading evidence images (from DB)
                    MediaSourceDisplay = r.MediaSource ?? "Local", // App name (Zoom, Google Chrome, etc.)
                    SerialNumber = 0,
                    DurationSeconds = r.Duration
                });
            }
        }

        public void RefreshResults()
        {
            _items.Clear();
            try
            {
                var list = DetectionResultsLoader.LoadFromResultsDir(_resultsDirectory ?? DetectionResultsLoader.GetDefaultResultsDir());
                foreach (var item in list)
                    _items.Add(item);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DetectionResultsScreen.RefreshResults: {ex.Message}");
            }
        }

        private void ResultsDirectory_Click(object sender, RoutedEventArgs e)
        {
            OpenResultsFolder();
        }

        /// <summary>
        /// Opens the results folder in the system file explorer.
        /// </summary>
        public void OpenResultsFolder()
        {
            string dir = _resultsDirectory ?? DetectionResultsLoader.GetDefaultResultsDir();
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            if (!Directory.Exists(dir))
            {
                MessageBox.Show("Results directory could not be created or found.", "Results", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Failed to open results folder: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_items == null || _items.Count == 0)
            {
                MessageBox.Show("No results to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = "X-PHY-Detection-Results.csv"
            };
            if (saveDialog.ShowDialog() != true)
                return;
            try
            {
                using (var w = new StreamWriter(saveDialog.FileName))
                {
                    w.WriteLine("Timestamp,Type,Result,Detection Confidence,Result Path");
                    foreach (var row in _items)
                    {
                        w.WriteLine(
                            "\"{0:yyyy-MM-dd HH:mm:ss}\",\"{1}\",\"{2}\",\"{3}%\",\"{4}\"",
                            row.Timestamp, row.Type, row.ResultText, row.ConfidencePercent,
                            (row.ResultPathOrId ?? "").Replace("\"", "\"\""));
                    }
                }
                MessageBox.Show("Results exported successfully.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                Process.Start("explorer.exe", "/select,\"" + saveDialog.FileName + "\"");
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Export failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewResult_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.DataContext is DetectionResultItem item)
            {
                var resultsDir = _resultsDirectory ?? DetectionResultsLoader.GetDefaultResultsDir();
                ShowSessionDetail(item, resultsDir);
            }
        }

        private void ShowSessionDetail(DetectionResultItem result, string resultsDirectory)
        {
            if (result == null) return;
            _sessionDetailsPanel.SetResult(result, resultsDirectory);
            ResultsListView.Visibility = Visibility.Collapsed;
            SessionDetailView.Visibility = Visibility.Visible;
        }

        /// <summary>Shows the Session Details panel for the given result (e.g. after "Stop & View Results" from notification).</summary>
        public void ShowSessionDetailForResult(DetectionResultItem result, string resultsDirectory)
        {
            if (result == null) return;
            _resultsDirectory = resultsDirectory ?? DetectionResultsLoader.GetDefaultResultsDir();
            SessionDetailPanel.SetResult(result, _resultsDirectory);
            ResultsListView.Visibility = Visibility.Collapsed;
            SessionDetailView.Visibility = Visibility.Visible;
        }
    }
}
