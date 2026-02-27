using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class UpdatePasswordComponent : UserControl
    {
        public event EventHandler PasswordUpdated;

        private readonly AuthService _authService;
        private readonly TokenStorage _tokenStorage;

        public UpdatePasswordComponent()
        {
            InitializeComponent();
            _authService = new AuthService();
            _tokenStorage = new TokenStorage();
        }

        public void ClearInputs()
        {
            CurrentPasswordBox.Password = "";
            CurrentPasswordReveal.Text = "";
            CurrentPasswordReveal.Visibility = Visibility.Collapsed;
            CurrentPasswordBox.Visibility = Visibility.Visible;
            NewPasswordBox.Password = "";
            NewPasswordReveal.Text = "";
            NewPasswordReveal.Visibility = Visibility.Collapsed;
            NewPasswordBox.Visibility = Visibility.Visible;
            ConfirmPasswordBox.Password = "";
            ConfirmPasswordReveal.Text = "";
            ConfirmPasswordReveal.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            ErrorMessageText.Text = "";
            ErrorMessageText.Visibility = Visibility.Collapsed;
            CurrentPasswordErrorText.Text = "";
            CurrentPasswordErrorText.Visibility = Visibility.Collapsed;
            NewPasswordErrorText.Text = "";
            NewPasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Text = "";
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
            UpdatePasswordPlaceholders();
            UpdateButtonState();
        }

        private void UpdatePasswordPlaceholders()
        {
            CurrentPasswordPlaceholder.Visibility = string.IsNullOrEmpty(CurrentPasswordBox.Password) && string.IsNullOrEmpty(CurrentPasswordReveal.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            NewPasswordPlaceholder.Visibility = string.IsNullOrEmpty(NewPasswordBox.Password) && string.IsNullOrEmpty(NewPasswordReveal.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            ConfirmPasswordPlaceholder.Visibility = string.IsNullOrEmpty(ConfirmPasswordBox.Password) && string.IsNullOrEmpty(ConfirmPasswordReveal.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateButtonState()
        {
            var current = CurrentPasswordBox.Password;
            var newPwd = NewPasswordReveal.Visibility == Visibility.Visible ? NewPasswordReveal.Text : NewPasswordBox.Password;
            var confirm = ConfirmPasswordReveal.Visibility == Visibility.Visible ? ConfirmPasswordReveal.Text : ConfirmPasswordBox.Password;
            UpdatePasswordButton.IsEnabled = !string.IsNullOrEmpty(current)
                && !string.IsNullOrEmpty(newPwd)
                && !string.IsNullOrEmpty(confirm)
                && newPwd.Length >= 8
                && confirm.Length >= 8
                && newPwd == confirm
                && IsValidPassword(newPwd);
        }

        private static bool IsValidPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Contains(" ")) return false;
            var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9\s])[^\s]{8,}$");
            return regex.IsMatch(password);
        }

        private void ValidateCurrentPassword()
        {
            var current = CurrentPasswordReveal.Visibility == Visibility.Visible ? CurrentPasswordReveal.Text : CurrentPasswordBox.Password;
            var isEmpty = string.IsNullOrEmpty(current);
            CurrentPasswordErrorText.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            CurrentPasswordErrorText.Text = isEmpty ? "Required." : "";
        }

        private void ValidateNewPassword()
        {
            var newPwd = NewPasswordReveal.Visibility == Visibility.Visible ? NewPasswordReveal.Text : NewPasswordBox.Password;
            var valid = !string.IsNullOrEmpty(newPwd) && IsValidPassword(newPwd);
            NewPasswordErrorText.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
            NewPasswordErrorText.Text = valid ? "" : "Min 8 chars, upper, lower, number, one special (no spaces).";
        }

        private void ValidateConfirmPassword()
        {
            var newPwd = NewPasswordReveal.Visibility == Visibility.Visible ? NewPasswordReveal.Text : NewPasswordBox.Password;
            var confirm = ConfirmPasswordReveal.Visibility == Visibility.Visible ? ConfirmPasswordReveal.Text : ConfirmPasswordBox.Password;
            var valid = !string.IsNullOrEmpty(confirm) && confirm == newPwd;
            ConfirmPasswordErrorText.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
            ConfirmPasswordErrorText.Text = valid ? "" : "Passwords do not match.";
        }

        private void CurrentPasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateCurrentPassword();
            UpdateButtonState();
        }

        private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (CurrentPasswordReveal.Visibility != Visibility.Visible)
                UpdatePasswordPlaceholders();
            ValidateCurrentPassword();
            UpdateButtonState();
        }

        private void CurrentPasswordReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CurrentPasswordReveal.Visibility == Visibility.Visible && CurrentPasswordBox.Password != CurrentPasswordReveal.Text)
                CurrentPasswordBox.Password = CurrentPasswordReveal.Text;
            UpdatePasswordPlaceholders();
            ValidateCurrentPassword();
            UpdateButtonState();
        }

        private void CurrentPasswordEye_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPasswordReveal.Visibility == Visibility.Visible)
            {
                CurrentPasswordBox.Password = CurrentPasswordReveal.Text;
                CurrentPasswordReveal.Visibility = Visibility.Collapsed;
                CurrentPasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                CurrentPasswordReveal.Text = CurrentPasswordBox.Password;
                CurrentPasswordReveal.Visibility = Visibility.Visible;
                CurrentPasswordBox.Visibility = Visibility.Collapsed;
            }
            UpdatePasswordPlaceholders();
        }

        private void NewPasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateNewPassword();
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (NewPasswordReveal.Visibility != Visibility.Visible)
                UpdatePasswordPlaceholders();
            ValidateNewPassword();
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void NewPasswordReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NewPasswordReveal.Visibility == Visibility.Visible && NewPasswordBox.Password != NewPasswordReveal.Text)
                NewPasswordBox.Password = NewPasswordReveal.Text;
            UpdatePasswordPlaceholders();
            ValidateNewPassword();
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void NewPasswordEye_Click(object sender, RoutedEventArgs e)
        {
            if (NewPasswordReveal.Visibility == Visibility.Visible)
            {
                NewPasswordBox.Password = NewPasswordReveal.Text;
                NewPasswordReveal.Visibility = Visibility.Collapsed;
                NewPasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                NewPasswordReveal.Text = NewPasswordBox.Password;
                NewPasswordReveal.Visibility = Visibility.Visible;
                NewPasswordBox.Visibility = Visibility.Collapsed;
            }
            UpdatePasswordPlaceholders();
        }

        private void NewPasswordInfoButton_Click(object sender, RoutedEventArgs e)
        {
            PasswordRequirementsPopup.IsOpen = true;
        }

        private void ConfirmPasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ConfirmPasswordReveal.Visibility != Visibility.Visible)
                UpdatePasswordPlaceholders();
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void ConfirmPasswordReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ConfirmPasswordReveal.Visibility == Visibility.Visible && ConfirmPasswordBox.Password != ConfirmPasswordReveal.Text)
                ConfirmPasswordBox.Password = ConfirmPasswordReveal.Text;
            UpdatePasswordPlaceholders();
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void ConfirmPasswordEye_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmPasswordReveal.Visibility == Visibility.Visible)
            {
                ConfirmPasswordBox.Password = ConfirmPasswordReveal.Text;
                ConfirmPasswordReveal.Visibility = Visibility.Collapsed;
                ConfirmPasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                ConfirmPasswordReveal.Text = ConfirmPasswordBox.Password;
                ConfirmPasswordReveal.Visibility = Visibility.Visible;
                ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            }
            UpdatePasswordPlaceholders();
        }

        private async void UpdatePassword_Click(object sender, RoutedEventArgs e)
        {
            var current = CurrentPasswordBox.Password;
            var newPwd = NewPasswordReveal.Visibility == Visibility.Visible ? NewPasswordReveal.Text : NewPasswordBox.Password;
            var confirm = ConfirmPasswordReveal.Visibility == Visibility.Visible ? ConfirmPasswordReveal.Text : ConfirmPasswordBox.Password;

            ErrorMessageText.Visibility = Visibility.Collapsed;
            CurrentPasswordErrorText.Visibility = Visibility.Collapsed;
            NewPasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;

            if (string.IsNullOrEmpty(current))
            {
                CurrentPasswordErrorText.Text = "Required.";
                CurrentPasswordErrorText.Visibility = Visibility.Visible;
                return;
            }
            ValidateNewPassword();
            if (NewPasswordErrorText.Visibility == Visibility.Visible)
                return;
            ValidateConfirmPassword();
            if (ConfirmPasswordErrorText.Visibility == Visibility.Visible)
                return;

            var tokens = _tokenStorage.GetTokens();
            if (tokens?.AccessToken == null)
            {
                ErrorMessageText.Text = "Session expired. Please sign in again.";
                ErrorMessageText.Visibility = Visibility.Visible;
                return;
            }

            UpdatePasswordButton.IsEnabled = false;
            try
            {
                await _authService.ChangePasswordAsync(current, newPwd, tokens.AccessToken);
                PasswordUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ErrorMessageText.Text = ToUserFriendlyError(ex.Message);
                ErrorMessageText.Visibility = Visibility.Visible;
                UpdatePasswordButton.IsEnabled = true;
            }
        }

        private static string ToUserFriendlyError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "Something went wrong. Please try again or contact support.";
            var m = message.Trim();
            if (m.IndexOf("current password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("invalid password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("Unauthorized", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Current password is incorrect. Please try again.";
            if (m.IndexOf("Network error", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Please check your internet connection and try again.";
            if (m.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Request timed out. Please check your connection and try again.";
            return message;
        }
    }
}
