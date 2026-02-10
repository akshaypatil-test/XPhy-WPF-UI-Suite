using System;
using System.IO;
using System.Windows;

namespace x_phy_wpf_ui
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // When the app is launched by the installer ("Launch when finished"), the process
            // current directory is often the installer's folder, not the app's. The native
            // video inference setup resolves the "models" path relative to the current directory.
            // Setting it here ensures models are always loaded from the application directory,
            // fixing "Environment Inference setup" failure on first run after install.
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(appDir) && Directory.Exists(appDir))
                {
                    Environment.CurrentDirectory = appDir;
                }
            }
            catch { /* non-fatal */ }

            base.OnStartup(e);
        }
    }
}
