using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace x_phy_wpf_ui
{
    public partial class FloatingWidgetWindow : Window
    {
        private readonly SolidColorBrush _greenBrush;
        private readonly SolidColorBrush _redBrush;
        private DoubleAnimation _rotateAnimation;
        private bool _isDetectionActive;
        private bool _isDeepfakeDetected;
        private Window _ownerWindow;

        public FloatingWidgetWindow()
        {
            InitializeComponent();
            _greenBrush = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)); // green
            _redBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0x15, 0x6B));   // red (X-PHY pink)
        }

        /// <summary>Set the main window so we can restore it on click.</summary>
        public void SetOwnerWindow(Window owner)
        {
            _ownerWindow = owner;
        }

        /// <summary>Show widget at bottom-right of working area.</summary>
        public void PositionAtBottomRight()
        {
            var workArea = System.Windows.SystemParameters.WorkArea;
            Left = workArea.Right - Width - 16;
            Top = workArea.Bottom - Height - 16;
        }

        /// <summary>Update detection state: ring rotates when active; green normally, red when deepfake detected.</summary>
        public void SetDetectionState(bool isDetecting, bool isDeepfakeDetected)
        {
            if (_isDetectionActive == isDetecting && _isDeepfakeDetected == isDeepfakeDetected)
                return;
            _isDetectionActive = isDetecting;
            _isDeepfakeDetected = isDeepfakeDetected;

            // Ring color: red when deepfake detected, otherwise green
            OuterRing.Stroke = _isDeepfakeDetected ? _redBrush : _greenBrush;

            var transform = OuterRing.RenderTransform as RotateTransform;
            if (transform == null) return;

            if (_isDetectionActive)
            {
                _rotateAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromSeconds(1.2),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                transform.BeginAnimation(RotateTransform.AngleProperty, _rotateAnimation);
            }
            else
            {
                transform.BeginAnimation(RotateTransform.AngleProperty, null);
                transform.Angle = 0;
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            double leftBefore = Left, topBefore = Top;
            try
            {
                DragMove(); // Makes the floater draggable; returns when user releases mouse
            }
            catch { }
            // If position didn't change (or barely), treat as click and restore main window
            const double clickThreshold = 8;
            double delta = Math.Abs(Left - leftBefore) + Math.Abs(Top - topBefore);
            if (delta < clickThreshold)
                RestoreOwner();
        }

        private void RestoreOwner()
        {
            if (_ownerWindow != null)
            {
                _ownerWindow.WindowState = WindowState.Normal;
                _ownerWindow.Activate();
            }
            Hide();
        }
    }
}
