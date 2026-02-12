using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class SettingsComponent : UserControl
    {
        public event EventHandler BackClicked;

        public SettingsComponent()
        {
            InitializeComponent();
            Loaded += SettingsComponent_Loaded;
        }

        private void SettingsComponent_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateThemeSelection();
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
                LightThemeText.Foreground = System.Windows.Media.Brushes.White;
                DarkThemeText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "Brush.TextPrimary");
                
                // Update icon colors
                LightThemeIcon.Fill = System.Windows.Media.Brushes.White;
                DarkThemeIcon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "Brush.TextPrimary");
            }
            else
            {
                // Dark theme is selected - gradient background
                LightThemeCard.Style = (Style)FindResource("ThemeCardStyle");
                DarkThemeCard.Style = (Style)FindResource("ThemeCardSelectedStyle");
                
                // Update text colors
                LightThemeText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "Brush.TextPrimary");
                DarkThemeText.Foreground = System.Windows.Media.Brushes.White;
                
                // Update icon colors
                LightThemeIcon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "Brush.TextPrimary");
                DarkThemeIcon.Fill = System.Windows.Media.Brushes.White;
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
            MessageBox.Show("You are running the latest version!", "Update Check", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
