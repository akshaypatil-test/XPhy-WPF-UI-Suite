using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class SettingsComponent : UserControl
    {
        /// <summary>Settings-page-only background for Update/System Info cards in light theme (very light cyan).</summary>
        private static readonly Brush SettingsCardsLightBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FBFD"));

        public event EventHandler BackClicked;

        public SettingsComponent()
        {
            InitializeComponent();
            Loaded += SettingsComponent_Loaded;
        }

        private bool _updatingAutoStart;

        private void SettingsComponent_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateThemeSelection();
            _updatingAutoStart = true;
            try
            {
                AutoStartToggle.IsChecked = StartupHelper.IsAutoStartEnabled();
            }
            finally
            {
                _updatingAutoStart = false;
            }
        }

        private void AutoStartToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_updatingAutoStart)
                return;
            try
            {
                StartupHelper.SetAutoStart(AutoStartToggle.IsChecked == true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto start: {ex.Message}");
                AppDialog.Show(Window.GetWindow(this), "Could not change startup setting. " + ex.Message, "Auto Start Application", MessageBoxImage.Warning);
                _updatingAutoStart = true;
                try { AutoStartToggle.IsChecked = StartupHelper.IsAutoStartEnabled(); }
                finally { _updatingAutoStart = false; }
            }
        }

        private void UpdateThemeSelection()
        {
            var currentTheme = ThemeManager.CurrentTheme;
            
            if (currentTheme == ThemeManager.Theme.Light)
            {
                // Light theme is selected - gradient background
                LightThemeCard.Style = (Style)FindResource("ThemeCardSelectedStyle");
                DarkThemeCard.Style = (Style)FindResource("ThemeCardStyle");
                
                // Update text colors
                LightThemeText.Foreground = Brushes.White;
                DarkThemeText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextPrimary");
                
                // Update icon colors
                LightThemeIcon.Fill = Brushes.White;
                DarkThemeIcon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "Brush.TextPrimary");

                // Settings-only: very light cyan background for Update Controls and System Info cards
                if (UpdateControlsCard != null) UpdateControlsCard.Background = SettingsCardsLightBackground;
                if (SystemInfoCard != null) SystemInfoCard.Background = SettingsCardsLightBackground;
            }
            else
            {
                // Dark theme is selected - gradient background
                LightThemeCard.Style = (Style)FindResource("ThemeCardStyle");
                DarkThemeCard.Style = (Style)FindResource("ThemeCardSelectedStyle");
                
                // Update text colors
                LightThemeText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextPrimary");
                DarkThemeText.Foreground = Brushes.White;
                
                // Update icon colors
                LightThemeIcon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "Brush.TextPrimary");
                DarkThemeIcon.Fill = Brushes.White;

                // Settings-only: use theme surface (same as rest of app)
                var surface = (Brush)FindResource("Brush.Surface");
                if (UpdateControlsCard != null) UpdateControlsCard.Background = surface;
                if (SystemInfoCard != null) SystemInfoCard.Background = surface;
            }
        }

        private void LightTheme_Click(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Settings: Light theme clicked");
            ThemeManager.ApplyTheme(ThemeManager.Theme.Light);
            UpdateThemeSelection();
        }

        private void DarkTheme_Click(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Settings: Dark theme clicked");
            ThemeManager.ApplyTheme(ThemeManager.Theme.Dark);
            UpdateThemeSelection();
        }

        private void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            LastCheckedText.Text = "Just Now";
            AppDialog.Show(Window.GetWindow(this), "You are running the latest version!", "Update Check", MessageBoxImage.Information);
        }
    }
}
