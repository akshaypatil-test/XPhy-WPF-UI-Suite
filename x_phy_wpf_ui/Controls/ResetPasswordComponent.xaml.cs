using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            Loaded += (s, e) =>
            {
                UpdateNewPasswordPlaceholder();
                UpdateConfirmPasswordPlaceholder();
            };
        }

        public void SetResetToken(string resetToken)
        {
            _resetToken = resetToken ?? string.Empty;
            NewPasswordBox.Password = "";
            NewPasswordRevealTextBox.Text = "";
            NewPasswordRevealTextBox.Visibility = Visibility.Collapsed;
            NewPasswordBox.Visibility = Visibility.Visible;
            UpdateEyeIcon(NewPasswordEyeButton, false);
            ConfirmPasswordBox.Password = "";
            ConfirmPasswordRevealTextBox.Text = "";
            ConfirmPasswordRevealTextBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            UpdateEyeIcon(ConfirmPasswordEyeButton, false);
            ErrorMessageText.Visibility = Visibility.Collapsed;
            NewPasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
            UpdateNewPasswordPlaceholder();
            UpdateConfirmPasswordPlaceholder();
            UpdateSubmitButtonState();
        }

        /// <summary>Clear all inputs and errors when navigating back to this screen.</summary>
        public void ClearInputs()
        {
            NewPasswordBox.Password = "";
            NewPasswordRevealTextBox.Text = "";
            NewPasswordRevealTextBox.Visibility = Visibility.Collapsed;
            NewPasswordBox.Visibility = Visibility.Visible;
            UpdateEyeIcon(NewPasswordEyeButton, false);
            ConfirmPasswordBox.Password = "";
            ConfirmPasswordRevealTextBox.Text = "";
            ConfirmPasswordRevealTextBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            UpdateEyeIcon(ConfirmPasswordEyeButton, false);
            ErrorMessageText.Text = "";
            ErrorMessageText.Visibility = Visibility.Collapsed;
            NewPasswordErrorText.Text = "";
            NewPasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Text = "";
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
            UpdateNewPasswordPlaceholder();
            UpdateConfirmPasswordPlaceholder();
            UpdateSubmitButtonState();
        }

        private static bool IsValidPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Contains(" "))
                return false;
            var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9\s])[^\s]{8,}$");
            return regex.IsMatch(password);
        }

        private string GetNewPassword()
        {
            return NewPasswordRevealTextBox.Visibility == Visibility.Visible
                ? NewPasswordRevealTextBox.Text ?? ""
                : NewPasswordBox.Password ?? "";
        }

        private string GetConfirmPassword()
        {
            return ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible
                ? ConfirmPasswordRevealTextBox.Text ?? ""
                : ConfirmPasswordBox.Password ?? "";
        }

        private void UpdateNewPasswordPlaceholder()
        {
            if (NewPasswordPlaceholder == null) return;
            var hasText = !string.IsNullOrEmpty(GetNewPassword());
            NewPasswordPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateConfirmPasswordPlaceholder()
        {
            if (ConfirmPasswordPlaceholder == null) return;
            var hasText = !string.IsNullOrEmpty(GetConfirmPassword());
            ConfirmPasswordPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        }

        private static void UpdateEyeIcon(Button eyeButton, bool isRevealed)
        {
            if (eyeButton?.Template == null) return;
            var iconShow = eyeButton.Template.FindName("IconShow", eyeButton) as System.Windows.UIElement;
            var iconHide = eyeButton.Template.FindName("IconHide", eyeButton) as System.Windows.UIElement;
            if (iconShow != null && iconHide != null)
            {
                iconShow.Visibility = isRevealed ? Visibility.Collapsed : Visibility.Visible;
                iconHide.Visibility = isRevealed ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void NewPasswordEyeButton_Click(object sender, RoutedEventArgs e)
        {
            if (NewPasswordRevealTextBox.Visibility == Visibility.Visible)
            {
                NewPasswordBox.Password = NewPasswordRevealTextBox.Text ?? "";
                NewPasswordRevealTextBox.Visibility = Visibility.Collapsed;
                NewPasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                NewPasswordRevealTextBox.Text = NewPasswordBox.Password ?? "";
                NewPasswordRevealTextBox.Visibility = Visibility.Visible;
                NewPasswordBox.Visibility = Visibility.Collapsed;
            }
            UpdateNewPasswordPlaceholder();
            UpdateEyeIcon(NewPasswordEyeButton, NewPasswordRevealTextBox.Visibility == Visibility.Visible);
            ValidateNewPassword();
            ValidateConfirmPassword();
            UpdateSubmitButtonState();
        }

        private void ConfirmPasswordEyeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible)
            {
                ConfirmPasswordBox.Password = ConfirmPasswordRevealTextBox.Text ?? "";
                ConfirmPasswordRevealTextBox.Visibility = Visibility.Collapsed;
                ConfirmPasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                ConfirmPasswordRevealTextBox.Text = ConfirmPasswordBox.Password ?? "";
                ConfirmPasswordRevealTextBox.Visibility = Visibility.Visible;
                ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            }
            UpdateConfirmPasswordPlaceholder();
            UpdateEyeIcon(ConfirmPasswordEyeButton, ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible);
            ValidateConfirmPassword();
            UpdateSubmitButtonState();
        }

        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (NewPasswordRevealTextBox.Visibility == Visibility.Visible)
                NewPasswordRevealTextBox.Text = NewPasswordBox.Password ?? "";
            UpdateNewPasswordPlaceholder();
            ValidateNewPassword();
            ValidateConfirmPassword();
            UpdateSubmitButtonState();
        }

        private void NewPasswordRevealTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NewPasswordRevealTextBox.Visibility == Visibility.Visible && NewPasswordBox.Password != NewPasswordRevealTextBox.Text)
                NewPasswordBox.Password = NewPasswordRevealTextBox.Text ?? "";
            UpdateNewPasswordPlaceholder();
            ValidateNewPassword();
            ValidateConfirmPassword();
            UpdateSubmitButtonState();
        }

        private void NewPasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateNewPassword();
            ValidateConfirmPassword();
            UpdateSubmitButtonState();
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible)
                ConfirmPasswordRevealTextBox.Text = ConfirmPasswordBox.Password ?? "";
            UpdateConfirmPasswordPlaceholder();
            ValidateConfirmPassword();
            UpdateSubmitButtonState();
        }

        private void ConfirmPasswordRevealTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible && ConfirmPasswordBox.Password != ConfirmPasswordRevealTextBox.Text)
                ConfirmPasswordBox.Password = ConfirmPasswordRevealTextBox.Text ?? "";
            UpdateConfirmPasswordPlaceholder();
            ValidateConfirmPassword();
            UpdateSubmitButtonState();
        }

        private void ConfirmPasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateConfirmPassword();
            UpdateSubmitButtonState();
        }

        private void ValidateNewPassword()
        {
            var pwd = GetNewPassword();
            if (string.IsNullOrEmpty(pwd))
            {
                NewPasswordErrorText.Text = "New password is required.";
                NewPasswordErrorText.Visibility = Visibility.Visible;
                SetFieldBorderError(NewPasswordFieldBorder, true);
                return;
            }
            if (pwd.Length < 8)
            {
                NewPasswordErrorText.Text = "Password must be at least 8 characters.";
                NewPasswordErrorText.Visibility = Visibility.Visible;
                SetFieldBorderError(NewPasswordFieldBorder, true);
                return;
            }
            if (pwd.Contains(" "))
            {
                NewPasswordErrorText.Text = "Spaces are not allowed in password.";
                NewPasswordErrorText.Visibility = Visibility.Visible;
                SetFieldBorderError(NewPasswordFieldBorder, true);
                return;
            }
            if (!IsValidPassword(pwd))
            {
                NewPasswordErrorText.Text = "Password must have uppercase, lowercase, number, and at least one special character.";
                NewPasswordErrorText.Visibility = Visibility.Visible;
                SetFieldBorderError(NewPasswordFieldBorder, true);
                return;
            }
            NewPasswordErrorText.Visibility = Visibility.Collapsed;
            SetFieldBorderError(NewPasswordFieldBorder, false);
        }

        private void ValidateConfirmPassword()
        {
            var confirm = GetConfirmPassword();
            var newPwd = GetNewPassword();
            if (string.IsNullOrEmpty(confirm))
            {
                ConfirmPasswordErrorText.Text = "Please confirm your password.";
                ConfirmPasswordErrorText.Visibility = Visibility.Visible;
                SetFieldBorderError(ConfirmPasswordFieldBorder, true);
                return;
            }
            if (confirm != newPwd)
            {
                ConfirmPasswordErrorText.Text = "Passwords do not match.";
                ConfirmPasswordErrorText.Visibility = Visibility.Visible;
                SetFieldBorderError(ConfirmPasswordFieldBorder, true);
                return;
            }
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
            SetFieldBorderError(ConfirmPasswordFieldBorder, false);
        }

        private static void SetFieldBorderError(Border border, bool hasError)
        {
            if (border == null) return;
            border.BorderBrush = hasError
                ? (Brush)Application.Current.FindResource("Brush.Error")
                : (Brush)Application.Current.FindResource("Brush.Border");
        }

        private void UpdateSubmitButtonState()
        {
            var newPwd = GetNewPassword();
            var confirm = GetConfirmPassword();
            SubmitButton.IsEnabled = !string.IsNullOrEmpty(_resetToken)
                && newPwd.Length >= 8
                && confirm.Length >= 8
                && newPwd == confirm
                && IsValidPassword(newPwd);
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            var newPwd = GetNewPassword();
            var confirm = GetConfirmPassword();

            NewPasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
            ErrorMessageText.Visibility = Visibility.Collapsed;
            SetFieldBorderError(NewPasswordFieldBorder, false);
            SetFieldBorderError(ConfirmPasswordFieldBorder, false);

            ValidateNewPassword();
            ValidateConfirmPassword();
            if (NewPasswordErrorText.Visibility == Visibility.Visible || ConfirmPasswordErrorText.Visibility == Visibility.Visible)
                return;

            if (!IsValidPassword(newPwd))
            {
                NewPasswordErrorText.Text = "Password must be at least 8 characters with uppercase, lowercase, number, and at least one special character. Spaces are not allowed.";
                NewPasswordErrorText.Visibility = Visibility.Visible;
                SetFieldBorderError(NewPasswordFieldBorder, true);
                return;
            }
            if (newPwd != confirm)
            {
                ConfirmPasswordErrorText.Text = "Passwords do not match.";
                ConfirmPasswordErrorText.Visibility = Visibility.Visible;
                SetFieldBorderError(ConfirmPasswordFieldBorder, true);
                return;
            }

            SubmitButton.IsEnabled = false;
            try
            {
                await _authService.ResetPasswordAsync(_resetToken, newPwd);
                AppDialog.Show(Window.GetWindow(this), "Your password has been reset successfully. You can now sign in.", "Password Reset", MessageBoxImage.Information);
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
