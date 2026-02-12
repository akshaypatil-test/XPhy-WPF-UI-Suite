using System;
using System.Windows;
using System.Windows.Media;

namespace x_phy_wpf_ui.Services
{
    /// <summary>
    /// Manages application theme switching between Light and Dark modes.
    /// </summary>
    public static class ThemeManager
    {
        public enum Theme
        {
            Dark,
            Light
        }

        public static Theme CurrentTheme { get; private set; } = Theme.Dark;

        public static void ApplyTheme(Theme theme)
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                // Load theme file
                var themeUri = theme == Theme.Dark 
                    ? "Resources/Themes/Dark.xaml" 
                    : "Resources/Themes/Light.xaml";
                
                var themeDict = new ResourceDictionary
                {
                    Source = new Uri(themeUri, UriKind.Relative)
                };

                // Update all Color resources from theme file
                foreach (var key in themeDict.Keys)
                {
                    var keyStr = key.ToString();
                    if (keyStr.StartsWith("Color."))
                    {
                        var color = (Color)themeDict[key];
                        app.Resources[key] = color;
                        
                        // Also create/update corresponding Brush
                        var brushKey = keyStr.Replace("Color.", "Brush.");
                        app.Resources[brushKey] = new SolidColorBrush(color);
                    }
                }

                CurrentTheme = theme;
                SaveThemePreference(theme);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThemeManager: Error - {ex.Message}");
            }
        }

        private static void SetColor(Application app, string key, string hex)
        {
            app.Resources[key] = (Color)ColorConverter.ConvertFromString(hex);
        }

        private static void SetBrush(Application app, string key, string hex)
        {
            app.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        /// <summary>
        /// Toggles between Dark and Light themes.
        /// </summary>
        public static void ToggleTheme()
        {
            var newTheme = CurrentTheme == Theme.Dark ? Theme.Light : Theme.Dark;
            ApplyTheme(newTheme);
        }

        /// <summary>
        /// Loads and applies the saved theme preference from settings.
        /// Call this on application startup.
        /// </summary>
        public static void LoadSavedTheme()
        {
            try
            {
                // Try to load from a simple text file in AppData
                var themeFile = GetThemePreferenceFilePath();
                if (System.IO.File.Exists(themeFile))
                {
                    var savedTheme = System.IO.File.ReadAllText(themeFile).Trim();
                    if (Enum.TryParse<Theme>(savedTheme, out var theme))
                    {
                        ApplyTheme(theme);
                        return;
                    }
                }
                
                // Default to Dark theme
                ApplyTheme(Theme.Dark);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThemeManager: Error loading theme preference - {ex.Message}");
                // If error occurs, default to Dark
                ApplyTheme(Theme.Dark);
            }
        }

        /// <summary>
        /// Saves the theme preference to a file.
        /// </summary>
        private static void SaveThemePreference(Theme theme)
        {
            try
            {
                var themeFile = GetThemePreferenceFilePath();
                var directory = System.IO.Path.GetDirectoryName(themeFile);
                
                // Ensure directory exists
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }
                
                System.IO.File.WriteAllText(themeFile, theme.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThemeManager: Error saving theme preference - {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the path to the theme preference file.
        /// </summary>
        private static string GetThemePreferenceFilePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = System.IO.Path.Combine(appData, "XPhyWpfUi");
            return System.IO.Path.Combine(appFolder, "theme.txt");
        }

    }
}
