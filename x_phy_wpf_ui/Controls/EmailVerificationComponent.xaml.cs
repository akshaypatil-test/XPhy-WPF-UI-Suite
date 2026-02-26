using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class EmailVerificationComponent : UserControl
    {
        public event EventHandler NavigateBack;
        public event EventHandler NavigateToAccountVerified;

        private readonly AuthService _authService;
        private string _email = string.Empty;
        private readonly TextBox[] _otpBoxes;

        public EmailVerificationComponent()
        {
            InitializeComponent();
            _authService = new AuthService();
            _otpBoxes = new[] { Otp0, Otp1, Otp2, Otp3, Otp4, Otp5 };
        }

        public void SetEmail(string email)
        {
            _email = email ?? string.Empty;
            foreach (var box in _otpBoxes)
                box.Clear();
            ErrorText.Visibility = Visibility.Collapsed;
            UpdateCreateAccountButtonState();
        }

        /// <summary>Clear OTP and errors when navigating back to this screen.</summary>
        public void ClearInputs()
        {
            foreach (var box in _otpBoxes)
                box.Clear();
            ErrorText.Text = "";
            ErrorText.Visibility = Visibility.Collapsed;
            UpdateCreateAccountButtonState();
        }

        private string GetOtpCode()
        {
            var code = "";
            foreach (var box in _otpBoxes)
                code += box.Text;
            return code;
        }

        private void UpdateCreateAccountButtonState()
        {
            CreateAccountButton.IsEnabled = GetOtpCode().Length == 6 && !string.IsNullOrWhiteSpace(_email);
        }

        private void Otp_TextChanged(object sender, TextChangedEventArgs e)
        {
            var box = (TextBox)sender;
            if (box.Text.Length > 1)
            {
                var ch = box.Text[box.Text.Length - 1];
                if (char.IsDigit(ch))
                    box.Text = ch.ToString();
                else
                    box.Text = "";
            }
            else if (box.Text.Length == 1 && !char.IsDigit(box.Text[0]))
                box.Text = "";

            if (box.Text.Length == 1)
            {
                var idx = Array.IndexOf(_otpBoxes, box);
                if (idx >= 0 && idx < _otpBoxes.Length - 1)
                    _otpBoxes[idx + 1].Focus();
            }
            UpdateCreateAccountButtonState();
        }

        private void Otp_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var box = (TextBox)sender;
            if (e.Key == Key.Back && string.IsNullOrEmpty(box.Text))
            {
                var idx = Array.IndexOf(_otpBoxes, box);
                if (idx > 0)
                {
                    _otpBoxes[idx - 1].Focus();
                    _otpBoxes[idx - 1].Clear();
                }
                e.Handled = true;
            }
        }

        private async void CreateAccount_Click(object sender, RoutedEventArgs e)
        {
            var code = GetOtpCode();
            if (code.Length != 6 || string.IsNullOrWhiteSpace(_email))
                return;
            ErrorText.Visibility = Visibility.Collapsed;
            CreateAccountButton.IsEnabled = false;
            CreateAccountButton.Content = "Verifying...";
            try
            {
                var response = await _authService.VerifyEmailAsync(_email, code);
                if (response.Success)
                    NavigateToAccountVerified?.Invoke(this, EventArgs.Empty);
                else
                {
                    ErrorText.Text = response.Message;
                    ErrorText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = ex.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
            finally
            {
                CreateAccountButton.Content = "Create Account";
                UpdateCreateAccountButtonState();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigateBack?.Invoke(this, EventArgs.Empty);
        }

        private void ResendCode_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            e.Handled = true;
            ResendCode_Click(sender, e);
        }

        private async void ResendCode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_email)) return;
            try
            {
                await _authService.ResendOtpAsync(_email);
                MessageBox.Show("A new code has been sent to your email.", "Code resent", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Resend failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
