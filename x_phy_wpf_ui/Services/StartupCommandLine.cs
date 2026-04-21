using System;

namespace x_phy_wpf_ui.Services
{
    /// <summary>
    /// Startup folder / Run entries pass <see cref="TrayAgentArgument"/> so Windows login launches the app as a tray agent (no main window) until the user opens it from the tray or a notification.
    /// </summary>
    public static class StartupCommandLine
    {
        public const string TrayAgentArgument = "--tray-agent";

        /// <summary>True when the process was started with <see cref="TrayAgentArgument"/> (e.g. from Startup shortcut).</summary>
        public static bool StartedAsTrayAgent { get; private set; }

        public static void Initialize(string[]? args)
        {
            StartedAsTrayAgent = false;
            if (args == null) return;
            foreach (var a in args)
            {
                if (string.Equals(a, TrayAgentArgument, StringComparison.OrdinalIgnoreCase))
                {
                    StartedAsTrayAgent = true;
                    return;
                }
            }
        }
    }
}
