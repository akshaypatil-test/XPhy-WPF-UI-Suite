using System;
using System.Windows;
using System.Windows.Controls;

namespace x_phy_wpf_ui.Controls
{
    public partial class PaymentSuccessPopup : UserControl
    {
        public event EventHandler CloseRequested;

        public PaymentSuccessPopup()
        {
            InitializeComponent();
        }

        public void SetDetails(string planName, int durationDays, decimal amount, string transactionId)
        {
            PlanNameText.Text = planName ?? "";
            AmountText.Text = $"${amount:F2}";
            TransactionIdText.Text = transactionId ?? "";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
