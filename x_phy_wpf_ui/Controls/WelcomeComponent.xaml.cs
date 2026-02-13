using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using x_phy_wpf_ui.Services;
using static x_phy_wpf_ui.Services.ThemeManager;

namespace x_phy_wpf_ui.Controls
{
    public partial class WelcomeComponent : UserControl
    {
        public event EventHandler NavigateToLaunch;

        private DispatcherTimer timer;

        public WelcomeComponent()
        {
            InitializeComponent();
            Loaded += WelcomeComponent_Loaded;
            
            // Create timer for 3 seconds
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(7);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void WelcomeComponent_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLogo();
        }

        private void UpdateLogo()
        {
            if (LogoImage == null) return;

            try
            {
                var isLight = ThemeManager.CurrentTheme == Theme.Light;
                var logoPath = isLight ? "/x-phy-inverted-logo.png" : "/x-phy.png";
                LogoImage.Source = new BitmapImage(new Uri(logoPath, UriKind.Relative));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WelcomeComponent: Error updating logo - {ex.Message}");
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            NavigateToLaunch?.Invoke(this, EventArgs.Empty);
        }
    }
}
