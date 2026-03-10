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

        /// <summary>Raised after the theme has been applied so UI (e.g. top bar logo) can refresh.</summary>
        public static event EventHandler ThemeChanged;

        public static void ApplyTheme(Theme theme)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"ThemeManager.ApplyTheme: Applying theme = {theme}");
                
                var app = Application.Current;
                if (app == null) return;

                // Load theme file
                var themeUri = theme == Theme.Dark 
                    ? "Resources/Themes/Dark.xaml" 
                    : "Resources/Themes/Light.xaml";
                
                System.Diagnostics.Debug.WriteLine($"ThemeManager.ApplyTheme: Loading theme file: {themeUri}");
                
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
                System.Diagnostics.Debug.WriteLine($"ThemeManager.ApplyTheme: Theme applied successfully. CurrentTheme is now: {CurrentTheme}");
                
                SaveThemePreference(theme);
                ThemeChanged?.Invoke(null, EventArgs.Empty);
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
        /// Loads and applies the saved theme preference. Same install: restores last selected theme. Fresh install or reinstall: defaults to Dark.
        /// Reinstall is detected by comparing install directory creation time (reinstall = new folder = new creation time).
        /// </summary>
        public static void LoadSavedTheme()
        {
            try
            {
                var appDataDir = GetAppDataFolder();
                var installMarkerPath = System.IO.Path.Combine(appDataDir, "install_marker.txt");
                var currentInstallDir = (AppDomain.CurrentDomain.BaseDirectory ?? "").TrimEnd('\\', '/');
                long currentDirCreationTimeTicks = 0;
                try
                {
                    currentDirCreationTimeTicks = System.IO.Directory.GetCreationTimeUtc(currentInstallDir).Ticks;
                }
                catch { }

                bool isSameInstall = false;
                if (System.IO.File.Exists(installMarkerPath))
                {
                    var lines = System.IO.File.ReadAllLines(installMarkerPath);
                    var storedPath = lines.Length > 0 ? (lines[0] ?? "").Trim() : "";
                    var storedTicks = lines.Length > 1 && long.TryParse(lines[1].Trim(), out var t) ? t : 0L;
                    if (!string.IsNullOrEmpty(storedPath) && storedPath.Equals(currentInstallDir, StringComparison.OrdinalIgnoreCase) && storedTicks == currentDirCreationTimeTicks)
                        isSameInstall = true;
                }

                if (!isSameInstall)
                {
                    System.Diagnostics.Debug.WriteLine("ThemeManager.LoadSavedTheme: Fresh install or reinstall, defaulting to Dark");
                    ApplyTheme(Theme.Dark);
                    try
                    {
                        if (!System.IO.Directory.Exists(appDataDir))
                            System.IO.Directory.CreateDirectory(appDataDir);
                        System.IO.File.WriteAllLines(installMarkerPath, new[] { currentInstallDir, currentDirCreationTimeTicks.ToString() });
                    }
                    catch { }
                    return;
                }

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

                ApplyTheme(Theme.Dark);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThemeManager: Error loading theme - {ex.Message}");
                ApplyTheme(Theme.Dark);
            }
        }

        /// <summary>Saves the theme preference and updates install marker (path + dir creation time) so same install is detected correctly.</summary>
        private static void SaveThemePreference(Theme theme)
        {
            try
            {
                var appDataDir = GetAppDataFolder();
                if (!System.IO.Directory.Exists(appDataDir))
                    System.IO.Directory.CreateDirectory(appDataDir);
                System.IO.File.WriteAllText(GetThemePreferenceFilePath(), theme.ToString());
                var currentInstallDir = (AppDomain.CurrentDomain.BaseDirectory ?? "").TrimEnd('\\', '/');
                long ticks = 0;
                try { ticks = System.IO.Directory.GetCreationTimeUtc(currentInstallDir).Ticks; }
                catch { }
                var installMarkerPath = System.IO.Path.Combine(appDataDir, "install_marker.txt");
                System.IO.File.WriteAllLines(installMarkerPath, new[] { currentInstallDir, ticks.ToString() });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThemeManager: Error saving theme preference - {ex.Message}");
            }
        }

        private static string GetAppDataFolder()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return System.IO.Path.Combine(appData, "XPhyWpfUi");
        }

        private static string GetThemePreferenceFilePath()
        {
            return System.IO.Path.Combine(GetAppDataFolder(), "theme.txt");
        }

    }
}
