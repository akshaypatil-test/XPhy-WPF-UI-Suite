using System;
using System.Windows;
using System.Windows.Controls;

namespace x_phy_wpf_ui.Controls
{
    public partial class AuthHostView : UserControl
    {
        public event EventHandler CloseRequested;

        public AuthHostView()
        {
            InitializeComponent();
        }

        public void SetContent(object content)
        {
            AuthContent.Content = content;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
                window.WindowState = WindowState.Minimized;
        }
    }
}
