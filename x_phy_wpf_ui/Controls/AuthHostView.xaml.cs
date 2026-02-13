using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class AuthHostView : UserControl
    {
        public event EventHandler CloseRequested;

        public AuthHostView()
        {
            InitializeComponent();
            Loaded += AuthHostView_Loaded;
            IsVisibleChanged += AuthHostView_IsVisibleChanged;
        }

        private void AuthHostView_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateBackgroundImage();
        }

        private void AuthHostView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                // When control becomes visible, update background based on current theme
                UpdateBackgroundImage();
            }
        }

        public void RefreshTheme()
        {
            UpdateBackgroundImage();
        }

        private void UpdateBackgroundImage()
        {
            if (MainBgImage == null) return;
            
            var currentTheme = ThemeManager.CurrentTheme;
            var isLight = currentTheme == ThemeManager.Theme.Light;
            
            System.Diagnostics.Debug.WriteLine($"AuthHostView: CurrentTheme = {currentTheme}, isLight = {isLight}");
            
            // Update background image
            var imageName = isLight ? "mainbg-white.png" : "mainbg.png";
            var uri = new Uri($"pack://application:,,,/{imageName}", UriKind.Absolute);
            System.Diagnostics.Debug.WriteLine($"AuthHostView: Loading background image: {imageName}");
            MainBgImage.Source = new BitmapImage(uri);
            
            // Update left panel overlay
            if (LeftPanelOverlay != null)
            {
                var overlayColor = isLight ? "#66FFFFFF" : "#CC000000"; // Light transparent white (40%), dark opaque black (80%)
                LeftPanelOverlay.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(overlayColor));
                System.Diagnostics.Debug.WriteLine($"AuthHostView: Set overlay color to: {overlayColor}");
            }
        }

        public void SetContent(object content)
        {
            AuthContent.Content = content;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
                window.WindowState = WindowState.Minimized;
        }
    }
}
