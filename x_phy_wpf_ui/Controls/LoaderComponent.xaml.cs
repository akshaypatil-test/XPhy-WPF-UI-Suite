using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace x_phy_wpf_ui.Controls
{
    public partial class LoaderComponent : UserControl
    {
        public static readonly DependencyProperty ArcOffsetAngleProperty = DependencyProperty.Register(
            nameof(ArcOffsetAngle), typeof(double), typeof(LoaderComponent),
            new PropertyMetadata(0.0, OnArcOffsetAngleChanged));

        private const double ArcCenterX = 60;
        private const double ArcCenterY = 60;
        private const double ArcRadius = 48;  // Just outside 72x72 icon (radius 36)
        private const double ArcSweepDegrees = 100;

        private Storyboard _rollStoryboard;

        public double ArcOffsetAngle
        {
            get => (double)GetValue(ArcOffsetAngleProperty);
            set => SetValue(ArcOffsetAngleProperty, value);
        }

        public LoaderComponent()
        {
            InitializeComponent();
            Loaded += LoaderComponent_Loaded;
            Unloaded += LoaderComponent_Unloaded;
        }

        private void LoaderComponent_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateRollingArcGeometry();
            StartRollingAnimation();
        }

        private void LoaderComponent_Unloaded(object sender, RoutedEventArgs e)
        {
            StopRollingAnimation();
        }

        private static void OnArcOffsetAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LoaderComponent loader)
                loader.UpdateRollingArcGeometry();
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

        private void StartRollingAnimation()
        {
            StopRollingAnimation();
            _rollStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var animation = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromMilliseconds(1200)));
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath(ArcOffsetAngleProperty));
            _rollStoryboard.Children.Add(animation);
            _rollStoryboard.Begin(this, true);
        }

        private void StopRollingAnimation()
        {
            _rollStoryboard?.Stop(this);
            _rollStoryboard = null;
        }
    }
}
