using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class DetectionResultsScreen : UserControl
    {
        private string _resultsDirectory;
        private ObservableCollection<DetectionResultItem> _items;
        private readonly SessionDetailsPanel _sessionDetailsPanel;
        private DispatcherTimer _exportToastTimer;
        /// <summary>True after first load (SetResultsFromApi or RefreshResults) so we don't flash "No results" before data arrives.</summary>
        private bool _hasLoadedResults;

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
            _items.CollectionChanged += (s, e) => UpdateNoResultsVisibility();
            // Before first load: show table (empty). Only show "No results" after we've loaded and count is 0.
            UpdateNoResultsVisibility();
        }

        private void UpdateNoResultsVisibility()
        {
            bool hasItems = _items != null && _items.Count > 0;
            bool showNoResults = _hasLoadedResults && !hasItems;
            if (NoResultsPanel != null)
                NoResultsPanel.Visibility = showNoResults ? Visibility.Visible : Visibility.Collapsed;
            if (ResultsTablePanel != null)
                ResultsTablePanel.Visibility = showNoResults ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ResultsListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv && e.Delta != 0)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
            }
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
                _hasLoadedResults = true;
                UpdateNoResultsVisibility();
                System.Diagnostics.Debug.WriteLine($"DetectionResultsScreen.SetResultsDirectoryAndRefresh: {ex.Message}");
            }
        }

        /// <summary>
        /// Populates the grid from the backend GetResults API response. Use when API is available.
        /// </summary>
        public void SetResultsFromApi(System.Collections.Generic.IEnumerable<ResultDto> results)
        {
            _items.Clear();
            if (results == null)
            {
                _hasLoadedResults = true;
                UpdateNoResultsVisibility();
                return;
            }
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
            _hasLoadedResults = true;
            UpdateNoResultsVisibility();
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
            _hasLoadedResults = true;
            UpdateNoResultsVisibility();
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

            string chosenPath = saveDialog.FileName;
            var itemsToExport = _items.ToList();

            // Show "Export Started" first and let it render, then run export and show "Export Completed"
            ShowExportToast("Export Started", "Detection Results Are Being Exported To Excel.", 0);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    using (var w = new StreamWriter(chosenPath))
                    {
                        w.WriteLine("Timestamp,Type,Result,Detection Confidence,Result Path");
                        foreach (var row in itemsToExport)
                        {
                            w.WriteLine(
                                "\"{0:yyyy-MM-dd HH:mm:ss}\",\"{1}\",\"{2}\",\"{3}%\",\"{4}\"",
                                row.Timestamp, row.Type, row.ResultText, row.ConfidencePercent,
                                (row.ResultPathOrId ?? "").Replace("\"", "\"\""));
                        }
                    }
                    ShowExportToast("Export Completed", "Results Have Been Exported Successfully.", 5);
                    Process.Start("explorer.exe", "/select,\"" + chosenPath + "\"");
                }
                catch (Exception ex)
                {
                    ExportToastCard.Visibility = Visibility.Collapsed;
                    _exportToastTimer?.Stop();
                    _exportToastTimer = null;
                    MessageBox.Show("Export failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void ShowExportToast(string title, string message, int autoCloseSeconds)
        {
            _exportToastTimer?.Stop();
            ExportToastTitle.Text = title ?? "";
            ExportToastMessage.Text = message ?? "";
            ExportToastCard.Visibility = Visibility.Visible;
            if (autoCloseSeconds > 0)
            {
                _exportToastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(autoCloseSeconds) };
                _exportToastTimer.Tick += (s, e) =>
                {
                    _exportToastTimer.Stop();
                    _exportToastTimer = null;
                    ExportToastCard.Visibility = Visibility.Collapsed;
                };
                _exportToastTimer.Start();
            }
        }

        private void ExportToastClose_Click(object sender, RoutedEventArgs e)
        {
            _exportToastTimer?.Stop();
            _exportToastTimer = null;
            ExportToastCard.Visibility = Visibility.Collapsed;
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
            _sessionDetailsPanel.SetResult(result, _resultsDirectory);
            ResultsListView.Visibility = Visibility.Collapsed;
            SessionDetailView.Visibility = Visibility.Visible;
        }

        /// <summary>Shows the results list and hides session detail. Call when navigating to Results so the list is shown instead of a previously opened record.</summary>
        public void ShowResultsList()
        {
            ResultsListView.Visibility = Visibility.Visible;
            SessionDetailView.Visibility = Visibility.Collapsed;
        }
    }
}
