using System;
using System.Windows;
using System.Windows.Controls;

namespace x_phy_wpf_ui.Controls
{
    public partial class ForgotUsernameSuccessComponent : UserControl
    {
        public event EventHandler NavigateToSignIn;

        public ForgotUsernameSuccessComponent()
        {
            InitializeComponent();
        }

        private void BackToSignIn_Click(object sender, RoutedEventArgs e)
        {
            NavigateToSignIn?.Invoke(this, EventArgs.Empty);
        }
    }
}
