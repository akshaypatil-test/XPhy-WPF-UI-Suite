using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class ResetPasswordComponent : UserControl
    {
        public event EventHandler NavigateBack;
        public event EventHandler NavigateToSignIn;

        private readonly AuthService _authService;
        private string _resetToken = string.Empty;

        public ResetPasswordComponent()
        {
            InitializeComponent();
            _authService = new AuthService();
        }

        public void SetResetToken(string resetToken)
        {
            _resetToken = resetToken ?? string.Empty;
            NewPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            ErrorMessageText.Visibility = Visibility.Collapsed;
            NewPasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
            UpdateSubmitButtonState();
        }

        /// <summary>Clear all inputs and errors when navigating back to this screen.</summary>
        public void ClearInputs()
        {
            NewPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            ErrorMessageText.Text = "";
            ErrorMessageText.Visibility = Visibility.Collapsed;
            NewPasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
            UpdateSubmitButtonState();
        }

        private static bool IsValidPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Contains(" "))
                return false;
            var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9\s])[^\s]{8,}$");
            return regex.IsMatch(password);
        }

        private void Password_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSubmitButtonState();
        }

        private void UpdateSubmitButtonState()
        {
            var newPwd = NewPasswordBox.Password;
            var confirm = ConfirmPasswordBox.Password;
            SubmitButton.IsEnabled = !string.IsNullOrEmpty(_resetToken)
                && newPwd.Length >= 8
                && confirm.Length >= 8
                && newPwd == confirm
                && IsValidPassword(newPwd);
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            var newPwd = NewPasswordBox.Password;
            var confirm = ConfirmPasswordBox.Password;

            NewPasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
            ErrorMessageText.Visibility = Visibility.Collapsed;

            if (!IsValidPassword(newPwd))
            {
                NewPasswordErrorText.Text = "Password must be at least 8 characters with uppercase, lowercase, number, and at least one special character. Spaces are not allowed.";
                NewPasswordErrorText.Visibility = Visibility.Visible;
                return;
            }
            if (newPwd != confirm)
            {
                ConfirmPasswordErrorText.Text = "Passwords do not match.";
                ConfirmPasswordErrorText.Visibility = Visibility.Visible;
                return;
            }

            SubmitButton.IsEnabled = false;
            try
            {
                await _authService.ResetPasswordAsync(_resetToken, newPwd);
                MessageBox.Show("Your password has been reset successfully. You can now sign in.", "Password Reset", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigateToSignIn?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ErrorMessageText.Text = ex.Message;
                ErrorMessageText.Visibility = Visibility.Visible;
                SubmitButton.IsEnabled = true;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigateBack?.Invoke(this, EventArgs.Empty);
        }
    }
}
