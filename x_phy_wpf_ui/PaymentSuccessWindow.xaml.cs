using System;
using System.Windows;

namespace x_phy_wpf_ui
{
    public partial class PaymentSuccessWindow : Window
    {
        public PaymentSuccessWindow(string planName, int durationDays, decimal amount, string transactionId)
        {
            InitializeComponent();

            PlanNameText.Text = planName;
            DurationText.Text = $"{durationDays} days";
            AmountText.Text = $"${amount:F2}";
            TransactionIdText.Text = transactionId;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Close StripePaymentWindow if it exists (e.g. when opened from legacy flow)
            foreach (Window window in Application.Current.Windows)
            {
                if (window is StripePaymentWindow)
                {
                    window.Close();
                    break;
                }
            }
            
            // Find and show MainWindow if it exists
            MainWindow mainWindow = null;
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mw)
                {
                    mainWindow = mw;
                    break;
                }
            }
            
            if (mainWindow != null)
            {
                mainWindow.Show();
                mainWindow.Activate();
            }
            else
            {
                // Create new MainWindow if none exists
                mainWindow = new MainWindow();
                mainWindow.Show();
            }
            
            this.Close();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }
    }
}
