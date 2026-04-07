#nullable enable
using System;
using System.Windows;
using System.Windows.Input;

namespace x_phy_wpf_ui
{
    /// <summary>
    /// In-app dialog for update check result (replaces Windows MessageBox).
    /// Size and layout similar to a standard Windows message box.
    /// </summary>
    public partial class UpdateCheckPopup : Window
    {
        public UpdateCheckPopup()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Show the popup with the given title and message. If owner is set, centers on owner.
        /// </summary>
        public void Show(string title, string message, Window? owner = null)
        {
            TitleText.Text = title;
            MessageText.Text = message;
            if (owner != null)
            {
                Owner = owner;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            Show();
            Activate();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
