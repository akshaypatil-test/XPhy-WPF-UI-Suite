using System.Reflection;

namespace x_phy_wpf_ui.Services
{
    /// <summary>User-facing app version (e.g. 2.01). Kept in sync with &lt;InformationalVersion&gt; in the project file.</summary>
    public static class ApplicationVersion
    {
        public const string DisplayVersion = "2.01";

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

            return DisplayVersion;
        }
    }
}
