using System.Windows;
using System.Windows.Controls;

namespace InstallerUI.Views
{
    public partial class InstallPathView : UserControl
    {
        public InstallPathView() => InitializeComponent();

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select installation folder"
            };
            if (DataContext is InstallerViewModel vm && !string.IsNullOrEmpty(vm.InstallPath))
                dlg.SelectedPath = vm.InstallPath;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (DataContext is InstallerViewModel v)
                    v.InstallPath = dlg.SelectedPath;
            }
        }

        private void LaunchOnStartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void QuickInstallRadio_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
