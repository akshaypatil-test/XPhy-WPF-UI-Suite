using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace x_phy_wpf_ui.Controls
{
    public partial class VerifyChangePasswordOtpDialog : UserControl
    {
        public event EventHandler? VerifyRequested;
        public event EventHandler? ResendRequested;
        public event EventHandler? CloseRequested;

        private readonly TextBox[] _otpBoxes;

        public VerifyChangePasswordOtpDialog()
        {
            InitializeComponent();
            _otpBoxes = new[] { Otp0, Otp1, Otp2, Otp3, Otp4, Otp5 };
            foreach (var box in _otpBoxes)
                box.PreviewTextInput += Otp_PreviewTextInput;
        }

        public string Code
        {
            get
            {
                var s = "";
                foreach (var box in _otpBoxes)
                    s += box.Text;
                return s;
            }
        }

        public void Clear()
        {
            foreach (var box in _otpBoxes)
                box.Text = "";
            ErrorText.Visibility = Visibility.Collapsed;
            ErrorText.Text = "";
            VerifyButton.IsEnabled = false;
        }

        public void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        }

        public void SetBusy(bool busy)
        {
            VerifyButton.IsEnabled = !busy && Code.Length == 6;
        }

        private void Otp_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]$");
        }

        private string GetCode()
        {
            var s = "";
            foreach (var box in _otpBoxes)
                s += box.Text;
            return s;
        }

        private void Otp_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox box && box.Text.Length > 1)
            {
                box.Text = box.Text.Substring(0, 1);
                box.CaretIndex = 1;
            }
            VerifyButton.IsEnabled = GetCode().Length == 6;
            var idx = Array.IndexOf(_otpBoxes, sender as TextBox);
            if (idx >= 0 && idx < _otpBoxes.Length - 1 && (sender as TextBox)?.Text?.Length == 1)
                _otpBoxes[idx + 1].Focus();
        }

        private void Otp_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Back)
                return;
            var box = sender as TextBox;
            if (box == null || box.Text.Length > 0)
                return;
            var idx = Array.IndexOf(_otpBoxes, box);
            if (idx > 0)
            {
                _otpBoxes[idx - 1].Focus();
                _otpBoxes[idx - 1].Clear();
            }
            e.Handled = true;
        }

        private void Verify_Click(object sender, RoutedEventArgs e) => VerifyRequested?.Invoke(this, EventArgs.Empty);

        private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

        private void Resend_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ResendRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
