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
            if (string.IsNullOrEmpty(password) || password.Length < 8) return false;
            var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$");
            return regex.IsMatch(password);
        }

        private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (CurrentPasswordReveal.Visibility != Visibility.Visible)
                UpdatePasswordPlaceholders();
            UpdateButtonState();
        }

        private void CurrentPasswordReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CurrentPasswordReveal.Visibility == Visibility.Visible && CurrentPasswordBox.Password != CurrentPasswordReveal.Text)
                CurrentPasswordBox.Password = CurrentPasswordReveal.Text;
            UpdatePasswordPlaceholders();
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

        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (NewPasswordReveal.Visibility != Visibility.Visible)
                UpdatePasswordPlaceholders();
            UpdateButtonState();
        }

        private void NewPasswordReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NewPasswordReveal.Visibility == Visibility.Visible && NewPasswordBox.Password != NewPasswordReveal.Text)
                NewPasswordBox.Password = NewPasswordReveal.Text;
            UpdatePasswordPlaceholders();
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
            MessageBox.Show(
                "Password must be at least 8 characters and contain:\n• Uppercase letter\n• Lowercase letter\n• Number\n• Special character (@$!%*?&)",
                "Password Requirements",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ConfirmPasswordReveal.Visibility != Visibility.Visible)
                UpdatePasswordPlaceholders();
            UpdateButtonState();
        }

        private void ConfirmPasswordReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ConfirmPasswordReveal.Visibility == Visibility.Visible && ConfirmPasswordBox.Password != ConfirmPasswordReveal.Text)
                ConfirmPasswordBox.Password = ConfirmPasswordReveal.Text;
            UpdatePasswordPlaceholders();
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
            NewPasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;

            if (!IsValidPassword(newPwd))
            {
                NewPasswordErrorText.Text = "Password must be at least 8 characters and contain uppercase, lowercase, number, and special character.";
                NewPasswordErrorText.Visibility = Visibility.Visible;
                return;
            }
            if (newPwd != confirm)
            {
                ConfirmPasswordErrorText.Text = "Passwords do not match.";
                ConfirmPasswordErrorText.Visibility = Visibility.Visible;
                return;
            }

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
                ErrorMessageText.Text = ex.Message;
                ErrorMessageText.Visibility = Visibility.Visible;
                UpdatePasswordButton.IsEnabled = true;
            }
        }
    }
}
