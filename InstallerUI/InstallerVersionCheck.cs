using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace InstallerUI
{
    /// <summary>
    /// Compares the bundled installer version (assembly from Directory.Build.props) to the installed product
    /// (Add/Remove Programs DisplayVersion, else file version of x_phy_wpf_ui.exe).
    /// </summary>
    public static class InstallerVersionCheck
    {
        /// <summary>Must match MSI Product / ARP DisplayName (vdproj ProductName).</summary>
        public const string ProductDisplayName = "X-PHY Deepfake Detector";

        /// <summary>Windows Installer: ERROR_INSTALL_ALREADY_EXISTS — newer or same product already installed.</summary>
        public const int MsiExitNewerOrSameProductExists = 1638;

        public static InstallVersionScenario EvaluateScenario()
        {
            if (!InstallerViewModel.IsAlreadyInstalled())
                return InstallVersionScenario.FreshInstall;

            var bundled = TryGetBundledVersion();
            if (bundled == null)
                return InstallVersionScenario.InstalledVersionUnknown;

            if (!TryGetBestEffortInstalledVersion(out var installed, out _))
                return InstallVersionScenario.InstalledVersionUnknown;

            // Single source of truth: parsed System.Version (registry/file vs assembly metadata). Avoid string-only compare.
            var cmp = installed.CompareTo(bundled);
            if (cmp == 0)
                return InstallVersionScenario.SameVersionInstalled;
            if (cmp < 0)
                return InstallVersionScenario.OlderVersionInstalled;
            return InstallVersionScenario.NewerVersionInstalled;
        }

        /// <summary>
        /// Human-readable version for this installer. Prefer <see cref="AssemblyInformationalVersionAttribute"/> (MSBuild / Directory.Build.props);
        /// PE <see cref="FileVersionInfo.ProductVersion"/> can stay stale and must not override the assembly bump.
        /// </summary>
        public static string TryGetBundledVersionString()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();

                var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                var s = info?.InformationalVersion?.Trim();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    var plus = s.IndexOf('+');
                    if (plus >= 0)
                        s = s.Substring(0, plus).Trim();
                    s = StripVersionNoise(s);
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }

                var path = asm.Location;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var vi = FileVersionInfo.GetVersionInfo(path);
                    var pv = (vi?.ProductVersion ?? "").Trim();
                    if (!string.IsNullOrEmpty(pv))
                    {
                        var plus = pv.IndexOf('+');
                        if (plus >= 0)
                            pv = pv.Substring(0, plus).Trim();
                        pv = StripVersionNoise(pv);
                        if (!string.IsNullOrEmpty(pv))
                            return pv;
                    }
                }

                return StripVersionNoise(asm.GetName().Version?.ToString());
            }
            catch
            {
                return null;
            }
        }

        public static Version TryGetBundledVersion()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                var s = info?.InformationalVersion?.Trim();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    var plus = s.IndexOf('+');
                    if (plus >= 0)
                        s = s.Substring(0, plus).Trim();
                    s = StripVersionNoise(s);
                    if (TryParseVersionFlexible(s, out var vInfo) && vInfo != null)
                        return vInfo;
                }

                var path = asm.Location;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var vi = FileVersionInfo.GetVersionInfo(path);
                    var pv = (vi?.ProductVersion ?? "").Trim();
                    if (!string.IsNullOrEmpty(pv))
                    {
                        var plus = pv.IndexOf('+');
                        if (plus >= 0)
                            pv = pv.Substring(0, plus).Trim();
                        pv = StripVersionNoise(pv);
                        if (TryParseVersionFlexible(pv, out var vFile) && vFile != null)
                            return vFile;
                    }
                }

                return asm.GetName().Version;
            }
            catch
            {
                return null;
            }
        }

        public static string GetSameVersionMessage()
        {
            var label = TryGetBundledVersionString() ?? TryGetBundledVersion()?.ToString() ?? "this version";
            return $"This version ({label}) is already installed.";
        }

        public static string GetDowngradeBlockedMessage()
        {
            TryGetBestEffortInstalledVersion(out var installedV, out var rawInstalled);
            var installedLabel = !string.IsNullOrWhiteSpace(rawInstalled) ? StripVersionNoise(rawInstalled) : (installedV?.ToString() ?? "unknown");
            var bundledLabel = TryGetBundledVersionString() ?? TryGetBundledVersion()?.ToString() ?? "this package";
            return $"A newer version ({installedLabel}) is already installed. This installer is {bundledLabel}. Downgrade is not supported. Remove the newer version from Settings > Apps if you need to install an older build.";
        }

        /// <summary>DisplayVersion from Uninstall, or file version of installed exe.</summary>
        public static bool TryGetBestEffortInstalledVersion(out Version version, out string rawLabel)
        {
            version = null;
            rawLabel = null;
            if (TryGetRegisteredDisplayVersionMax(out version, out rawLabel))
                return true;

            var exePath = TryFindInstalledExeForVersionRead();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return false;

            try
            {
                var vi = FileVersionInfo.GetVersionInfo(exePath);
                rawLabel = !string.IsNullOrWhiteSpace(vi.ProductVersion) ? vi.ProductVersion.Trim() : vi.FileVersion?.Trim();
                if (string.IsNullOrWhiteSpace(rawLabel))
                    return false;
                var plus = rawLabel.IndexOf('+');
                if (plus >= 0)
                    rawLabel = rawLabel.Substring(0, plus).Trim();
                return TryParseVersionFlexible(rawLabel, out version) && version != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetVersionForExeAtPath(string installDir, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(installDir))
                return false;
            var exePath = Path.Combine(installDir.TrimEnd('\\', '/'), InstallerViewModel.AppExeName);
            if (!File.Exists(exePath))
                return false;
            try
            {
                var vi = FileVersionInfo.GetVersionInfo(exePath);
                var s = !string.IsNullOrWhiteSpace(vi.ProductVersion) ? vi.ProductVersion.Trim() : vi.FileVersion?.Trim();
                if (string.IsNullOrWhiteSpace(s))
                    return false;
                var plus = s.IndexOf('+');
                if (plus >= 0)
                    s = s.Substring(0, plus).Trim();
                return TryParseVersionFlexible(s, out version) && version != null;
            }
            catch
            {
                return false;
            }
        }

        public static int CompareToBundled(Version installed)
        {
            if (installed == null)
                return 0;
            var bundled = TryGetBundledVersion();
            if (bundled == null)
                return 0;
            return installed.CompareTo(bundled);
        }

        /// <summary>Best registered DisplayVersion among all uninstall rows matching this product (max version).</summary>
        private static bool TryGetRegisteredDisplayVersionMax(out Version version, out string raw)
        {
            version = null;
            raw = null;
            Version best = null;
            string bestRaw = null;

            void Consider(RegistryKey baseKey)
            {
                if (baseKey == null)
                    return;
                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using (var subKey = baseKey.OpenSubKey(subKeyName))
                        {
                            if (subKey == null)
                                continue;
                            var displayName = subKey.GetValue("DisplayName") as string;
                            if (string.IsNullOrWhiteSpace(displayName) ||
                                !displayName.Trim().Equals(ProductDisplayName, StringComparison.OrdinalIgnoreCase))
                                continue;
                            var dv = subKey.GetValue("DisplayVersion") as string;
                            if (string.IsNullOrWhiteSpace(dv))
                                continue;
                            var trimmed = StripVersionNoise(dv.Trim());
                            if (!TryParseVersionFlexible(trimmed, out var v) || v == null)
                                continue;
                            if (best == null || v.CompareTo(best) > 0)
                            {
                                best = v;
                                bestRaw = trimmed;
                            }
                        }
                    }
                    catch
                    {
                        /* next */
                    }
                }
            }

            var views = new[] { RegistryView.Registry64, RegistryView.Registry32 };
            foreach (var view in views)
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view)
                               .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        Consider(baseKey);
                    }
                }
                catch { /* ignore */ }
            }

            try
            {
                using (var baseKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    Consider(baseKey);
            }
            catch { /* ignore */ }

            if (best == null)
                return false;
            version = best;
            raw = bestRaw;
            return true;
        }

        /// <summary>
        /// Resolves the installed app exe for file-based version when ARP has no usable DisplayVersion.
        /// Prefer the uninstall row for <see cref="ProductDisplayName"/> (InstallLocation), then the same
        /// ordered paths as <see cref="InstallerViewModel.TryResolveInstalledExePath"/> for Quick Install.
        /// Do not scan arbitrary <c>Program Files\X-PHY\*</c> siblings — that can pick a dev copy with a
        /// higher file version and incorrectly treat a newer installer as a downgrade.
        /// </summary>
        private static string TryFindInstalledExeForVersionRead()
        {
            if (TryGetInstallLocationExeForProductDisplayName(out var fromUninstall))
                return fromUninstall;

            var (_, exe) = InstallerViewModel.TryResolveInstalledExePath(InstallerViewModel.GetDefaultInstallPath());
            return !string.IsNullOrEmpty(exe) && File.Exists(exe) ? exe : null;
        }

        private static bool TryGetInstallLocationExeForProductDisplayName(out string exePath)
        {
            exePath = null;
            var views = new[] { RegistryView.Registry64, RegistryView.Registry32 };
            foreach (var view in views)
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view)
                               .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (TryUninstallMatchingInstallLocation(baseKey, out exePath))
                            return true;
                    }
                }
                catch { /* ignore */ }
            }

            try
            {
                using (var baseKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (TryUninstallMatchingInstallLocation(baseKey, out exePath))
                        return true;
                }
            }
            catch { /* ignore */ }

            return false;
        }

        private static bool TryUninstallMatchingInstallLocation(RegistryKey baseKey, out string exePath)
        {
            exePath = null;
            if (baseKey == null)
                return false;
            foreach (var subKeyName in baseKey.GetSubKeyNames())
            {
                try
                {
                    using (var subKey = baseKey.OpenSubKey(subKeyName))
                    {
                        if (subKey == null)
                            continue;
                        var displayName = subKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName) ||
                            !displayName.Trim().Equals(ProductDisplayName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        var installLocation = subKey.GetValue("InstallLocation") as string;
                        if (string.IsNullOrWhiteSpace(installLocation))
                            continue;
                        var dir = installLocation.TrimEnd('\\', '/');
                        var candidate = Path.Combine(dir, InstallerViewModel.AppExeName);
                        if (!File.Exists(candidate))
                            continue;
                        exePath = candidate;
                        return true;
                    }
                }
                catch { /* next */ }
            }

            return false;
        }

        private static bool TryParseVersionFlexible(string s, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(s))
                return false;
            s = ExtractLeadingDottedVersion(StripVersionNoise(s.Trim()));
            // MSI / vdproj often used "2.01" for "2.0.1". System.Version parses "2.01" as 2.1, which breaks comparisons vs 2.0.2.
            if (TryParseLegacyMsiTwoDigitPatch(s, out version))
                return true;
            if (Version.TryParse(s, out version))
                return true;
            return false;
        }

        private static string StripVersionNoise(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (c == '\u200B' || c == '\u200C' || c == '\u200D' || c == '\uFEFF' || c == '\u00A0')
                    continue;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        /// <summary>Leading dotted numeric segment only (stops before first non-numeric segment).</summary>
        private static string ExtractLeadingDottedVersion(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return s;
            s = StripVersionNoise(s.Trim()).Replace(',', '.');
            var m = Regex.Match(s, @"^\d+(\.\d+)*");
            return m.Success ? m.Value : Regex.Replace(s, @"[^\d.]", "");
        }

        /// <summary>Maps "2.01" style (major.twoDigitPatch) to 2.0.1 when the second segment is two digits starting with 0.</summary>
        private static bool TryParseLegacyMsiTwoDigitPatch(string s, out Version version)
        {
            version = null;
            var parts = s.Split('.');
            if (parts.Length != 2)
                return false;
            if (!int.TryParse(parts[0], out var major))
                return false;
            if (parts[1].Length != 2 || parts[1][0] != '0')
                return false;
            if (!int.TryParse(parts[1], out var patch))
                return false;
            try
            {
                version = new Version(major, 0, patch, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
