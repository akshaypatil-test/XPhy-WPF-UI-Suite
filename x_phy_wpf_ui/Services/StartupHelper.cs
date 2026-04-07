using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace x_phy_wpf_ui.Services
{
    /// <summary>
    /// "Run at login" via Startup folder shortcut (same name as the product — Windows Settings shows it correctly).
    /// Migrates legacy HKCU\...\Run entries from older builds.
    /// </summary>
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "X-PHY Deepfake Detector";
        private const string StartupShortcutFileName = "X-PHY Deepfake Detector.lnk";
        private const string ShortcutDescription = "X-PHY Deepfake Detector";

        private static string StartupFolder =>
            Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        private static string StartupShortcutPath => Path.Combine(StartupFolder, StartupShortcutFileName);

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

        public static bool IsAutoStartEnabled()
        {
            try
            {
                if (File.Exists(StartupShortcutPath))
                    return true;
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false))
                {
                    var value = key?.GetValue(AppRegistryName) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartupHelper IsAutoStartEnabled: {ex.Message}");
                return false;
            }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                if (enable)
                {
                    RemoveLegacyRunKey();
                    var exePath = GetExecutablePath();
                    if (string.IsNullOrEmpty(exePath))
                        return;
                    var workingDir = Path.GetDirectoryName(exePath);
                    Directory.CreateDirectory(StartupFolder);
                    if (!TryCreateShortcut(StartupShortcutPath, exePath, workingDir))
                        throw new InvalidOperationException("Could not create startup shortcut.");
                }
                else
                {
                    try
                    {
                        if (File.Exists(StartupShortcutPath))
                            File.Delete(StartupShortcutPath);
                    }
                    catch { /* ignore */ }
                    RemoveLegacyRunKey();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartupHelper SetAutoStart: {ex.Message}");
                throw;
            }
        }

        private static void RemoveLegacyRunKey()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
                {
                    key?.DeleteValue(AppRegistryName, throwOnMissingValue: false);
                }
            }
            catch { /* ignore */ }
        }

        private static bool TryCreateShortcut(string shortcutPath, string exePath, string workingDir)
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;
                var shell = Activator.CreateInstance(shellType);
                var shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });
                if (shortcut == null) return false;
                var t = shortcut.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir ?? "" });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { ShortcutDescription });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                return File.Exists(shortcutPath);
            }
            catch
            {
                return false;
            }
        }
    }
}
