using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using x_phy_wpf_ui.Services;
using static x_phy_wpf_ui.Services.ThemeManager;

namespace x_phy_wpf_ui.Controls
{
    public partial class StartDetectionCard : UserControl
    {
        public static readonly DependencyProperty StatusTextProperty =
            DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(StartDetectionCard), 
                new PropertyMetadata("Ready to start detection", OnStatusTextChanged));

        public string StatusText
        {
            get => (string)GetValue(StatusTextProperty);
            set => SetValue(StatusTextProperty, value);
        }

        public event EventHandler<RoutedEventArgs> StartDetectionClicked;

        public StartDetectionCard()
        {
            InitializeComponent();
            Loaded += StartDetectionCard_Loaded;
            IsVisibleChanged += StartDetectionCard_IsVisibleChanged;
        }

        private void StartDetectionCard_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBackgroundImage();
        }

        private void StartDetectionCard_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                UpdateBackgroundImage();
            }
        }

        private void UpdateBackgroundImage()
        {
            if (BackgroundImageBrush == null) return;

            try
            {
                var isLight = ThemeManager.CurrentTheme == Theme.Light;
                var imagePath = isLight ? "pack://application:,,,/facebg-white.png" : "pack://application:,,,/facebg.png";
                
                BackgroundImageBrush.ImageSource = new BitmapImage(new Uri(imagePath));
                
                System.Diagnostics.Debug.WriteLine($"StartDetectionCard: Updated background to {(isLight ? "facebg-white.png" : "facebg.png")}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartDetectionCard: Error updating background - {ex.Message}");
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartDetectionClicked?.Invoke(this, e);
        }

        private static void OnStatusTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StartDetectionCard control && control.DetectionStatusText != null)
            {
                control.DetectionStatusText.Text = e.NewValue?.ToString() ?? "Ready to start detection";
            }
        }
    }
}
