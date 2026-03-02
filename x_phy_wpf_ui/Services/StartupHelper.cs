using System;
using System.IO;
using Microsoft.Win32;

namespace x_phy_wpf_ui.Services
{
    /// <summary>
    /// Manages "run at Windows startup" via HKCU\...\Run. No admin required.
    /// </summary>
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "X-PHY Deepfake Detector";

        /// <summary>
        /// Gets the full path to the current process executable (for writing to Run key).
        /// </summary>
        public static string GetExecutablePath()
        {
            try
            {
                var path = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartupHelper GetExecutablePath: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Returns true if the app is currently set to run at Windows startup.
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false))
                {
                    var value = key?.GetValue(AppName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartupHelper IsAutoStartEnabled: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Enables or disables running the app at Windows startup.
        /// </summary>
        public static void SetAutoStart(bool enable)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
                {
                    if (key == null)
                        return;
                    if (enable)
                    {
                        var exePath = GetExecutablePath();
                        if (string.IsNullOrEmpty(exePath))
                            return;
                        key.SetValue(AppName, exePath);
                    }
                    else
                    {
                        key.DeleteValue(AppName, throwOnMissingValue: false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartupHelper SetAutoStart: {ex.Message}");
                throw;
            }
        }
    }
}
