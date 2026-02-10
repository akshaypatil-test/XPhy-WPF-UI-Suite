using System;
using System.Windows;
using System.Windows.Controls;

namespace x_phy_wpf_ui.Controls
{
    public partial class AccountVerifiedComponent : UserControl
    {
        public event EventHandler NavigateToSignIn;

        public AccountVerifiedComponent()
        {
            InitializeComponent();
        }

        private void SignIn_Click(object sender, RoutedEventArgs e)
        {
            NavigateToSignIn?.Invoke(this, EventArgs.Empty);
        }
    }
}
