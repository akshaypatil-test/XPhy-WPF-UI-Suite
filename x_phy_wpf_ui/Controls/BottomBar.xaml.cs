using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace x_phy_wpf_ui.Controls
{
    public partial class BottomBar : UserControl
    {
        private DispatcherTimer _loaderTimer;
        private double _rotationAngle = 0;

        public event EventHandler SubscribeClicked;
        public event EventHandler SupportClicked;
        public event EventHandler LogoutClicked;

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(
                nameof(Status),
                typeof(string),
                typeof(BottomBar),
                new PropertyMetadata("Trial", OnStatusChanged));

        public static readonly DependencyProperty RemainingDaysProperty =
            DependencyProperty.Register(
                nameof(RemainingDays),
                typeof(int),
                typeof(BottomBar),
                new PropertyMetadata(30, OnRemainingDaysChanged));

        public static readonly DependencyProperty ShowSubscribeButtonProperty =
            DependencyProperty.Register(
                nameof(ShowSubscribeButton),
                typeof(bool),
                typeof(BottomBar),
                new PropertyMetadata(true, OnShowSubscribeButtonChanged));

        public static readonly DependencyProperty ShowContactAdminButtonProperty =
            DependencyProperty.Register(
                nameof(ShowContactAdminButton),
                typeof(bool),
                typeof(BottomBar),
                new PropertyMetadata(false, OnShowContactAdminButtonChanged));

        public static readonly DependencyProperty AttemptsProperty =
            DependencyProperty.Register(
                nameof(Attempts),
                typeof(int?),
                typeof(BottomBar),
                new PropertyMetadata(null, OnAttemptsChanged));

        public string Status
        {
            get => (string)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public int RemainingDays
        {
            get => (int)GetValue(RemainingDaysProperty);
            set => SetValue(RemainingDaysProperty, value);
        }

        public bool ShowSubscribeButton
        {
            get => (bool)GetValue(ShowSubscribeButtonProperty);
            set => SetValue(ShowSubscribeButtonProperty, value);
        }

        /// <summary>When true, show Contact Administrator button (disabled for now). Used for Corp user when license is Expired.</summary>
        public bool ShowContactAdminButton
        {
            get => (bool)GetValue(ShowContactAdminButtonProperty);
            set => SetValue(ShowContactAdminButtonProperty, value);
        }

        public int? Attempts
        {
            get => (int?)GetValue(AttemptsProperty);
            set => SetValue(AttemptsProperty, value);
        }

        public BottomBar()
        {
            InitializeComponent();
            UpdateDisplay();
            
            // Initialize loader timer
            _loaderTimer = new DispatcherTimer();
            _loaderTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _loaderTimer.Tick += LoaderTimer_Tick;
        }

        private void LoaderTimer_Tick(object sender, EventArgs e)
        {
            if (LoaderRotateTransform != null)
            {
                _rotationAngle += 6; // Rotate 6 degrees per tick
                if (_rotationAngle >= 360)
                {
                    _rotationAngle = 0;
                }
                LoaderRotateTransform.Angle = _rotationAngle;
            }
        }

        private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BottomBar bottomBar)
            {
                bottomBar.UpdateStatusDisplay();
            }
        }

        private static void OnRemainingDaysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BottomBar bottomBar)
            {
                bottomBar.UpdateRemainingDaysDisplay();
            }
        }

        private static void OnShowSubscribeButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BottomBar bottomBar)
            {
                bool show = (bool)e.NewValue;
                bottomBar.SubscribeButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void OnShowContactAdminButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BottomBar bottomBar && bottomBar.ContactAdminButton != null)
            {
                bottomBar.ContactAdminButton.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static void OnAttemptsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BottomBar bottomBar)
            {
                bottomBar.UpdateAttemptsDisplay();
            }
        }

        private void UpdateDisplay()
        {
            UpdateStatusDisplay();
            UpdateRemainingDaysDisplay();
            UpdateAttemptsDisplay();
            SubscribeButton.Visibility = ShowSubscribeButton ? Visibility.Visible : Visibility.Collapsed;
            if (ContactAdminButton != null)
                ContactAdminButton.Visibility = ShowContactAdminButton ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateStatusDisplay()
        {
            if (StatusText == null) return;

            StatusText.Text = Status;

            // Set color based on status
            switch (Status.ToLower())
            {
                case "active":
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                    break;
                case "trial":
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(27, 180, 204)); // Teal
                    break;
                case "expired":
                case "no license":
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                    break;
                default:
                    StatusText.Foreground = new SolidColorBrush(Color.FromRgb(27, 180, 204)); // Teal (default)
                    break;
            }
            // When Expired: hide Remain days and Attempts; show only Status Expired and Subscribe Now
            if (RemainDaysPanel != null)
                RemainDaysPanel.Visibility = Status?.Equals("Expired", StringComparison.OrdinalIgnoreCase) == true ? Visibility.Collapsed : Visibility.Visible;
            UpdateAttemptsDisplay();
        }

        private void UpdateRemainingDaysDisplay()
        {
            if (RemainDaysText == null) return;

            RemainDaysText.Text = RemainingDays.ToString();

            // Set color based on status
            if (Status.ToLower() == "active")
            {
                RemainDaysText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            }
            else if (Status.ToLower() == "trial")
            {
                RemainDaysText.Foreground = new SolidColorBrush(Color.FromRgb(27, 180, 204)); // Teal
            }
            else
            {
                RemainDaysText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            }
        }

        private void UpdateAttemptsDisplay()
        {
            if (AttemptsText == null || AttemptsPanel == null) return;

            if (Status?.Equals("Trial", StringComparison.OrdinalIgnoreCase) == true && Attempts.HasValue)
            {
                AttemptsPanel.Visibility = Visibility.Visible;
                AttemptsText.Text = Attempts.Value.ToString();
                AttemptsText.Foreground = new SolidColorBrush(Color.FromRgb(27, 180, 204)); // Teal
            }
            else
            {
                AttemptsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SubscribeButton_Click(object sender, RoutedEventArgs e)
        {
            SubscribeClicked?.Invoke(this, EventArgs.Empty);
        }

        private void SupportButton_Click(object sender, RoutedEventArgs e)
        {
            SupportClicked?.Invoke(this, EventArgs.Empty);
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            LogoutClicked?.Invoke(this, EventArgs.Empty);
        }

        public void ShowLogoutLoader()
        {
            if (LogoutLoader != null)
            {
                LogoutLoader.Visibility = Visibility.Visible;
                if (LogoutButton != null)
                {
                    LogoutButton.IsEnabled = false;
                    LogoutButton.Opacity = 0.6;
                }
                
                // Start rotation animation
                _rotationAngle = 0;
                if (LoaderRotateTransform != null)
                {
                    LoaderRotateTransform.Angle = 0;
                }
                _loaderTimer?.Start();
            }
        }

        public void HideLogoutLoader()
        {
            if (LogoutLoader != null)
            {
                LogoutLoader.Visibility = Visibility.Collapsed;
                if (LogoutButton != null)
                {
                    LogoutButton.IsEnabled = true;
                    LogoutButton.Opacity = 1.0;
                }
                
                // Stop rotation animation
                _loaderTimer?.Stop();
            }
        }
    }
}
