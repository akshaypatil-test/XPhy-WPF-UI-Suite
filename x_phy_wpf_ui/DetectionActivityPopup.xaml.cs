#nullable enable
using System;
using System.Windows;
using System.Windows.Media;

namespace x_phy_wpf_ui
{
    /// <summary>
    /// Popup shown when user clicks the floating widget: "Cancel detection" and "Stop & View results".
    /// </summary>
    public partial class DetectionActivityPopup : Window
    {
        private Action? _onCancelDetection;
        private Action? _onStopAndViewResults;

        public DetectionActivityPopup()
        {
            InitializeComponent();
        }

        public void SetActions(Action onCancelDetection, Action onStopAndViewResults)
        {
            _onCancelDetection = onCancelDetection;
            _onStopAndViewResults = onStopAndViewResults;
        }

        /// <summary>Set status dot green (normal) or red (deepfake detected).</summary>
        public void SetDeepfakeDetected(bool isDeepfakeDetected)
        {
            StatusDot.Fill = isDeepfakeDetected
                ? new SolidColorBrush(Color.FromRgb(0xE2, 0x15, 0x6B))
                : new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
        }

        private double _anchorLeft, _anchorTop;

        /// <summary>Show popup near the given position (e.g. above the floating widget).</summary>
        public void ShowNear(double widgetLeft, double widgetTop)
        {
            _anchorLeft = widgetLeft;
            _anchorTop = widgetTop;
            Loaded += PositionPopup;
            Show();
            Activate();
        }

        private void PositionPopup(object sender, RoutedEventArgs e)
        {
            Loaded -= PositionPopup;
            var work = SystemParameters.WorkArea;
            Left = _anchorLeft - Width - 8;
            Top = _anchorTop - ActualHeight - 8;
            if (Top < work.Top) Top = work.Top + 8;
            if (Left < work.Left) Left = work.Left + 8;
            if (Left + Width > work.Right) Left = work.Right - Width - 8;
        }

        private void CancelDetection_Click(object sender, RoutedEventArgs e)
        {
            try { _onCancelDetection?.Invoke(); } catch { }
            Close();
        }

        private void StopAndViewResults_Click(object sender, RoutedEventArgs e)
        {
            try { _onStopAndViewResults?.Invoke(); } catch { }
            Close();
        }
    }
}
