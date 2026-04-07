using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace x_phy_wpf_ui
{
    public partial class FloatingWidgetWindow : Window
    {
        public static readonly DependencyProperty ArcOffsetAngleProperty = DependencyProperty.Register(
            nameof(ArcOffsetAngle), typeof(double), typeof(FloatingWidgetWindow),
            new PropertyMetadata(0.0, OnArcOffsetAngleChanged));

        private const double ArcCenterX = 40;
        private const double ArcCenterY = 40;
        private const double ArcRadius = 36;
        private const double ArcSweepDegrees = 100;

        private readonly SolidColorBrush _greenBrush;
        private readonly SolidColorBrush _redBrush;
        private Storyboard _rollStoryboard;
        private bool _isDetectionActive;
        private bool _isDeepfakeDetected;
        private Window _ownerWindow;
        private Action _onCancelDetection;
        private Action _onStopAndViewResults;
        private DetectionActivityPopup _activityPopup;

        public double ArcOffsetAngle
        {
            get => (double)GetValue(ArcOffsetAngleProperty);
            set => SetValue(ArcOffsetAngleProperty, value);
        }

        public FloatingWidgetWindow()
        {
            InitializeComponent();
            _greenBrush = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)); // green
            _redBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0x15, 0x6B));   // red (X-PHY pink)
            _onCancelDetection = () => { };
            _onStopAndViewResults = () => { };
            Loaded += (s, e) => { UpdateRollingArcGeometry(); };
        }

        private static void OnArcOffsetAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FloatingWidgetWindow w)
                w.UpdateRollingArcGeometry();
        }

        private void UpdateRollingArcGeometry()
        {
            if (RollingArcPath == null) return;
            double angle = ArcOffsetAngle * Math.PI / 180.0;
            double endAngle = (ArcOffsetAngle + ArcSweepDegrees) * Math.PI / 180.0;
            double startX = ArcCenterX + ArcRadius * Math.Cos(angle);
            double startY = ArcCenterY + ArcRadius * Math.Sin(angle);
            double endX = ArcCenterX + ArcRadius * Math.Cos(endAngle);
            double endY = ArcCenterY + ArcRadius * Math.Sin(endAngle);
            var pathFigure = new PathFigure(
                new Point(startX, startY),
                new PathSegment[] { new ArcSegment(new Point(endX, endY), new Size(ArcRadius, ArcRadius), 0, ArcSweepDegrees > 180, SweepDirection.Clockwise, true) },
                false);
            var geometry = new PathGeometry(new[] { pathFigure });
            RollingArcPath.Data = geometry;
        }

        /// <summary>Set the main window so we can restore it on click.</summary>
        public void SetOwnerWindow(Window owner)
        {
            _ownerWindow = owner;
        }

        /// <summary>Set actions for the Detection Activity popup: Cancel detection and Stop & View results.</summary>
        public void SetActions(Action onCancelDetection, Action onStopAndViewResults)
        {
            _onCancelDetection = onCancelDetection ?? (() => { });
            _onStopAndViewResults = onStopAndViewResults ?? (() => { });
        }

        /// <summary>Show widget at bottom-right of working area.</summary>
        public void PositionAtBottomRight()
        {
            var workArea = System.Windows.SystemParameters.WorkArea;
            Left = workArea.Right - Width - 16;
            Top = workArea.Bottom - Height - 16;
        }

        /// <summary>Bring the floating launcher above notification windows when a notification appears.</summary>
        public void BringAboveNotifications()
        {
            if (!IsVisible) return;
            try
            {
                Topmost = false;
                Topmost = true;
                Activate();
            }
            catch { }
        }

        /// <summary>Update detection state: arc loader runs when active; green normally, red when deepfake detected (same smooth animation as Loader screen).</summary>
        public void SetDetectionState(bool isDetecting, bool isDeepfakeDetected)
        {
            bool wasActive = _isDetectionActive;
            _isDetectionActive = isDetecting;
            _isDeepfakeDetected = isDeepfakeDetected;

            if (RollingArcPath == null) return;

            // Arc color: red when deepfake detected, otherwise green (always update so it stays in sync with notification)
            RollingArcPath.Stroke = _isDeepfakeDetected ? _redBrush : _greenBrush;

            if (_isDetectionActive)
            {
                // Only start rolling animation when transitioning to active (avoid restarting every tick)
                if (!wasActive)
                {
                    _rollStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
                    var animation = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromMilliseconds(1200)));
                    Storyboard.SetTarget(animation, this);
                    Storyboard.SetTargetProperty(animation, new PropertyPath(ArcOffsetAngleProperty));
                    _rollStoryboard.Children.Add(animation);
                    _rollStoryboard.Begin(this, true);
                }
            }
            else
            {
                _rollStoryboard?.Stop(this);
                _rollStoryboard = null;
                ArcOffsetAngle = 0;
                UpdateRollingArcGeometry();
                // Close Detection Activity popup when detection ends (duration past or stopped) so it doesn't stay on screen
                CloseDetectionActivityPopup();
            }
        }

        /// <summary>Close the Detection Activity popup if it is open (e.g. when detection duration is past and user didn't click an option).</summary>
        public void CloseDetectionActivityPopup()
        {
            try
            {
                if (_activityPopup != null && _activityPopup.IsLoaded)
                {
                    _activityPopup.Close();
                    _activityPopup = null;
                }
            }
            catch { }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            double leftBefore = Left, topBefore = Top;
            try
            {
                DragMove(); // Makes the floater draggable; returns when user releases mouse
            }
            catch { }
            // If position didn't change (or barely), treat as click and show Detection Activity popup
            const double clickThreshold = 8;
            double delta = Math.Abs(Left - leftBefore) + Math.Abs(Top - topBefore);
            if (delta < clickThreshold)
                ShowDetectionActivityPopup();
        }

        private void ShowDetectionActivityPopup()
        {
            // If popup is already open, close it (toggle off) instead of opening again
            if (_activityPopup != null && _activityPopup.IsVisible)
            {
                CloseDetectionActivityPopup();
                return;
            }
            CloseDetectionActivityPopup();
            _activityPopup = new DetectionActivityPopup();
            _activityPopup.Closed += (s, e) => { _activityPopup = null; };
            _activityPopup.SetActions(_onCancelDetection, _onStopAndViewResults);
            _activityPopup.SetDeepfakeDetected(_isDeepfakeDetected);
            _activityPopup.ShowNear(Left + Width, Top + Height / 2);
        }
    }
}
