using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace x_phy_wpf_ui.Services
{
    /// <summary>Persists update-check time and whether a newer version was reported but not yet installed.</summary>
    public static class UpdateCheckStateStore
    {
        private const string FileName = "update-check-state.json";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            Formatting = Formatting.Indented
        };

        private static string GetPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "X-PHY", "X-PHY Deepfake Detector", FileName);
        }

        private static Dto LoadDto()
        {
            try
            {
                var path = GetPath();
                if (!File.Exists(path))
                    return new Dto();
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<Dto>(json, JsonSettings) ?? new Dto();
            }
            catch
            {
                return new Dto();
            }
        }

        private static void SaveDto(Dto dto)
        {
            try
            {
                var path = GetPath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonConvert.SerializeObject(dto, JsonSettings));
            }
            catch
            {
                /* non-fatal */
            }
        }

        public static DateTime? LoadLastCheckUtc()
        {
            var dto = LoadDto();
            if (dto.LastCheckUtc == null)
                return null;
            var t = dto.LastCheckUtc.Value;
            return t.Kind == DateTimeKind.Utc ? t : DateTime.SpecifyKind(t.ToUniversalTime(), DateTimeKind.Utc);
        }

        /// <summary>Network or HTTP failure: record last check time only; keep prior "upgrade pending" if any.</summary>
        public static void PersistFailedCheck(DateTime lastCheckUtc)
        {
            try
            {
                var dto = LoadDto();
                dto.LastCheckUtc = NormalizeUtc(lastCheckUtc);
                SaveDto(dto);
            }
            catch
            {
                /* non-fatal */
            }
        }

        /// <summary>Successful API response: set or clear pending upgrade from server.</summary>
        public static void PersistSuccessfulCheck(DateTime lastCheckUtc, bool updateAvailable, string latestVersion)
        {
            try
            {
                var dto = LoadDto();
                dto.LastCheckUtc = NormalizeUtc(lastCheckUtc);
                if (updateAvailable && !string.IsNullOrWhiteSpace(latestVersion))
                    dto.PendingUpgradeLatestVersion = latestVersion.Trim();
                else
                    dto.PendingUpgradeLatestVersion = null;
                SaveDto(dto);
            }
            catch
            {
                /* non-fatal */
            }
        }

        /// <summary>True if a prior successful check reported a newer version and the running app is still older.</summary>
        public static bool IsUpgradePendingVersusCurrent(string currentDisplayVersion)
        {
            try
            {
                var dto = LoadDto();
                var pending = dto.PendingUpgradeLatestVersion;
                if (string.IsNullOrWhiteSpace(pending))
                    return false;

                var cmp = CompareDisplayVersions(currentDisplayVersion, pending);
                if (cmp >= 0)
                {
                    dto.PendingUpgradeLatestVersion = null;
                    SaveDto(dto);
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static DateTime NormalizeUtc(DateTime utcNow)
        {
            return utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
        }

        /// <summary>Prefer <see cref="Version"/> for numeric segments; otherwise ordinal.</summary>
        private static int CompareDisplayVersions(string a, string b)
        {
            a = NormalizeVersionToken(a);
            b = NormalizeVersionToken(b);
            if (Version.TryParse(a, out var va) && Version.TryParse(b, out var vb))
                return va.CompareTo(vb);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeVersionToken(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "";
            s = s.Trim();
            var plus = s.IndexOf('+');
            if (plus >= 0)
                s = s.Substring(0, plus).Trim();
            return s;
        }

        public static string FormatLastCheckedDisplay(DateTime? lastCheckUtc)
        {
            if (!lastCheckUtc.HasValue)
                return "Never";

            var local = lastCheckUtc.Value.Kind == DateTimeKind.Utc
                ? lastCheckUtc.Value.ToLocalTime()
                : lastCheckUtc.Value.ToLocalTime();

            var now = DateTime.Now;
            var delta = now - local;
            if (delta.TotalSeconds < 45)
                return "Just now";
            if (delta.TotalMinutes < 60)
                return $"{(int)delta.TotalMinutes} minute{((int)delta.TotalMinutes == 1 ? "" : "s")} ago";
            if (delta.TotalHours < 24 && local.Date == now.Date)
                return $"Today, {local:h:mm tt}";
            if (local.Date == now.Date.AddDays(-1))
                return $"Yesterday, {local:h:mm tt}";
            return local.ToString("MMMM d, yyyy, h:mm tt", CultureInfo.CurrentCulture);
        }

        private sealed class Dto
        {
            public DateTime? LastCheckUtc { get; set; }
            /// <summary>Latest version string from the server when an update was offered.</summary>
            public string PendingUpgradeLatestVersion { get; set; }
        }
    }
}
