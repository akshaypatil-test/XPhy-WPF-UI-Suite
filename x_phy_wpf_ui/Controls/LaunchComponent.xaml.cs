using System;
using System.Windows;
using System.Windows.Controls;

namespace x_phy_wpf_ui.Controls
{
    public partial class LaunchComponent : UserControl
    {
        public event EventHandler NavigateToSignIn;
        public event EventHandler NavigateToCreateAccount;

        public LaunchComponent()
        {
            InitializeComponent();
        }

        private void SignIn_Click(object sender, RoutedEventArgs e)
        {
            NavigateToSignIn?.Invoke(this, EventArgs.Empty);
        }

        private void CreateAccount_Click(object sender, RoutedEventArgs e)
        {
            NavigateToCreateAccount?.Invoke(this, EventArgs.Empty);
        }

        private void CorporateSignIn_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            NavigateToSignIn?.Invoke(this, EventArgs.Empty);
        }
    }
}
