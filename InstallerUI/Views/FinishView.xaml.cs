using System;
using System.Windows;
using System.Windows.Controls;

namespace InstallerUI.Views
{
    public partial class FinishView : UserControl
    {
        public FinishView()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                if (DataContext is InstallerViewModel vm)
                {
                    if (vm.InstallSucceeded)
                    {
                        TitleText.Text = "Installation Complete";
                        MessageText.Text = "X-PHY Deepfake Detector has been installed successfully. Click Finish to exit.";
                        LaunchNowCheckBox.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        var msg = vm.FailureMessage ?? "An error occurred during installation.";
                        TitleText.Text = msg.IndexOf("already installed", StringComparison.OrdinalIgnoreCase) >= 0
                            ? "Already Installed"
                            : "Installation Failed";
                        MessageText.Text = msg;
                        LaunchNowCheckBox.Visibility = Visibility.Collapsed;
                    }
                }
            };
        }

        private void LaunchNowCheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
