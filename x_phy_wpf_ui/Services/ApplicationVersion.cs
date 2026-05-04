using System;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace x_phy_wpf_ui.Services
{
    /// <summary>User-facing app version from assembly metadata (set in Directory.Build.props).</summary>
    public static class ApplicationVersion
    {
        public static string GetDisplayVersion()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                var s = attr?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(s))
                {
                    var plus = s.IndexOf('+');
                    if (plus >= 0)
                        s = s.Substring(0, plus).Trim();
                    return s;
                }
            }
            catch { /* use fallback */ }

            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                if (v != null)
                    return v.ToString();
            }
            catch { /* ignore */ }

            return "0.0.0";
        }

        /// <summary>Build date from AssemblyMetadata (ReleaseDate in Directory.Build.props, yyyy-MM-dd) displayed as YYYYMMDD.</summary>
        public static string GetReleaseDateDisplay()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var meta = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => string.Equals(a.Key, "ReleaseDate", StringComparison.OrdinalIgnoreCase));
                var raw = meta?.Value?.Trim();
                if (string.IsNullOrEmpty(raw))
                    return "—";

                if (DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    return d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

                if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d2))
                    return d2.ToLocalTime().ToString("yyyyMMdd", CultureInfo.InvariantCulture);

                return raw;
            }
            catch
            {
                return "—";
            }
        }
    }
}
