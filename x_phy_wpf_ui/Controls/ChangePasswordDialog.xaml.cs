using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace x_phy_wpf_ui.Controls
{
    public partial class ChangePasswordDialog : UserControl
    {
        public event EventHandler? BackRequested;
        public event EventHandler? UpdatePasswordRequested;

        private static readonly Regex PasswordRegex = new Regex(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$");

        public ChangePasswordDialog()
        {
            InitializeComponent();
        }

        public string CurrentPassword => CurrentReveal.Visibility == Visibility.Visible ? CurrentReveal.Text : CurrentPasswordBox.Password;
        public string NewPassword => NewReveal.Visibility == Visibility.Visible ? NewReveal.Text : NewPasswordBox.Password;
        public string ConfirmPassword => ConfirmReveal.Visibility == Visibility.Visible ? ConfirmReveal.Text : ConfirmPasswordBox.Password;

        public bool Validate(out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(CurrentPassword))
            {
                error = "Enter your current password.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                error = "Enter a new password.";
                return false;
            }
            if (!PasswordRegex.IsMatch(NewPassword))
            {
                error = "Password must be at least 8 characters and contain uppercase, lowercase, number, and special character (@$!%*?&).";
                return false;
            }
            if (NewPassword != ConfirmPassword)
            {
                error = "Passwords do not match.";
                return false;
            }
            if (CurrentPassword == NewPassword)
            {
                error = "New password must be different from current password.";
                return false;
            }
            return true;
        }

        /// <summary>Clears all password fields and error. Call when opening the dialog so previous data does not persist.</summary>
        public void Clear()
        {
            CurrentPasswordBox.Password = "";
            CurrentReveal.Text = "";
            CurrentReveal.Visibility = Visibility.Collapsed;
            CurrentPasswordBox.Visibility = Visibility.Visible;

            NewPasswordBox.Password = "";
            NewReveal.Text = "";
            NewReveal.Visibility = Visibility.Collapsed;
            NewPasswordBox.Visibility = Visibility.Visible;

            ConfirmPasswordBox.Password = "";
            ConfirmReveal.Text = "";
            ConfirmReveal.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Visible;

            CurrentErrorText.Visibility = Visibility.Collapsed;
            NewErrorText.Visibility = Visibility.Collapsed;
            ConfirmErrorText.Visibility = Visibility.Collapsed;
            SetBorderErrorState(CurrentBorder, false);
            SetBorderErrorState(NewBorder, false);
            SetBorderErrorState(ConfirmBorder, false);
            NewPasswordRequirementsPopup.IsOpen = false;
            ClearAndHideError();
            UpdatePlaceholders();
            UpdateButtonState();
        }

        public void ClearAndHideError()
        {
            ErrorText.Visibility = Visibility.Collapsed;
            ErrorText.Text = "";
        }

        public void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        public void SetBusy(bool busy)
        {
            UpdatePasswordButton.IsEnabled = !busy;
        }

        private void UpdatePlaceholders()
        {
            CurrentPlaceholder.Visibility = string.IsNullOrEmpty(CurrentPasswordBox.Password) && string.IsNullOrEmpty(CurrentReveal.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            NewPlaceholder.Visibility = string.IsNullOrEmpty(NewPasswordBox.Password) && string.IsNullOrEmpty(NewReveal.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            ConfirmPlaceholder.Visibility = string.IsNullOrEmpty(ConfirmPasswordBox.Password) && string.IsNullOrEmpty(ConfirmReveal.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateButtonState()
        {
            var cur = CurrentReveal.Visibility == Visibility.Visible ? CurrentReveal.Text : CurrentPasswordBox.Password;
            var @new = NewReveal.Visibility == Visibility.Visible ? NewReveal.Text : NewPasswordBox.Password;
            var conf = ConfirmReveal.Visibility == Visibility.Visible ? ConfirmReveal.Text : ConfirmPasswordBox.Password;
            UpdatePasswordButton.IsEnabled = !string.IsNullOrEmpty(cur) && !string.IsNullOrEmpty(@new) && !string.IsNullOrEmpty(conf)
                && @new.Length >= 8 && conf.Length >= 8 && @new == conf && PasswordRegex.IsMatch(@new);
        }

        private void SetBorderErrorState(System.Windows.Controls.Border border, bool hasError)
        {
            if (border == null) return;
            if (hasError)
            {
                border.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Error");
                border.BorderThickness = new Thickness(1);
            }
            else
            {
                border.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Border");
                border.BorderThickness = new Thickness(1);
            }
        }

        private void NewPasswordInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (NewPasswordRequirementsPopup != null)
                NewPasswordRequirementsPopup.IsOpen = !NewPasswordRequirementsPopup.IsOpen;
        }

        private void ValidateCurrentPassword()
        {
            var cur = CurrentReveal.Visibility == Visibility.Visible ? CurrentReveal.Text : CurrentPasswordBox.Password;
            if (string.IsNullOrWhiteSpace(cur))
            {
                CurrentErrorText.Text = "Current password is required.";
                CurrentErrorText.Visibility = Visibility.Visible;
                SetBorderErrorState(CurrentBorder, true);
            }
            else
            {
                CurrentErrorText.Visibility = Visibility.Collapsed;
                SetBorderErrorState(CurrentBorder, false);
            }
        }

        private void ValidateNewPassword()
        {
            var @new = NewReveal.Visibility == Visibility.Visible ? NewReveal.Text : NewPasswordBox.Password;
            if (string.IsNullOrWhiteSpace(@new))
            {
                NewErrorText.Text = "New password is required.";
                NewErrorText.Visibility = Visibility.Visible;
                SetBorderErrorState(NewBorder, true);
            }
            else if (!PasswordRegex.IsMatch(@new))
            {
                NewErrorText.Text = "Password must be at least 8 characters and contain uppercase, lowercase, number, and special character (@$!%*?&).";
                NewErrorText.Visibility = Visibility.Visible;
                SetBorderErrorState(NewBorder, true);
            }
            else
            {
                NewErrorText.Visibility = Visibility.Collapsed;
                SetBorderErrorState(NewBorder, false);
            }
        }

        private void ValidateConfirmPassword()
        {
            var @new = NewReveal.Visibility == Visibility.Visible ? NewReveal.Text : NewPasswordBox.Password;
            var conf = ConfirmReveal.Visibility == Visibility.Visible ? ConfirmReveal.Text : ConfirmPasswordBox.Password;
            if (string.IsNullOrWhiteSpace(conf))
            {
                ConfirmErrorText.Text = "Please confirm your password.";
                ConfirmErrorText.Visibility = Visibility.Visible;
                SetBorderErrorState(ConfirmBorder, true);
            }
            else if (@new != conf)
            {
                ConfirmErrorText.Text = "Passwords do not match.";
                ConfirmErrorText.Visibility = Visibility.Visible;
                SetBorderErrorState(ConfirmBorder, true);
            }
            else
            {
                ConfirmErrorText.Visibility = Visibility.Collapsed;
                SetBorderErrorState(ConfirmBorder, false);
            }
        }

        private void CurrentPassword_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateCurrentPassword();
            UpdateButtonState();
        }

        private void NewPassword_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateNewPassword();
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void ConfirmPassword_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (CurrentReveal.Visibility != Visibility.Visible)
                UpdatePlaceholders();
            ValidateCurrentPassword();
            UpdateButtonState();
        }

        private void CurrentReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CurrentReveal.Visibility == Visibility.Visible && CurrentPasswordBox.Password != CurrentReveal.Text)
                CurrentPasswordBox.Password = CurrentReveal.Text;
            UpdatePlaceholders();
            ValidateCurrentPassword();
            UpdateButtonState();
        }

        private void CurrentEye_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentReveal.Visibility == Visibility.Visible)
            {
                CurrentPasswordBox.Password = CurrentReveal.Text;
                CurrentReveal.Visibility = Visibility.Collapsed;
                CurrentPasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                CurrentReveal.Text = CurrentPasswordBox.Password;
                CurrentReveal.Visibility = Visibility.Visible;
                CurrentPasswordBox.Visibility = Visibility.Collapsed;
            }
            UpdatePlaceholders();
        }

        private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (NewReveal.Visibility != Visibility.Visible)
                UpdatePlaceholders();
            ValidateNewPassword();
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void NewReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NewReveal.Visibility == Visibility.Visible && NewPasswordBox.Password != NewReveal.Text)
                NewPasswordBox.Password = NewReveal.Text;
            UpdatePlaceholders();
            ValidateNewPassword();
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void NewEye_Click(object sender, RoutedEventArgs e)
        {
            if (NewReveal.Visibility == Visibility.Visible)
            {
                NewPasswordBox.Password = NewReveal.Text;
                NewReveal.Visibility = Visibility.Collapsed;
                NewPasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                NewReveal.Text = NewPasswordBox.Password;
                NewReveal.Visibility = Visibility.Visible;
                NewPasswordBox.Visibility = Visibility.Collapsed;
            }
            UpdatePlaceholders();
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ConfirmReveal.Visibility != Visibility.Visible)
                UpdatePlaceholders();
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void ConfirmReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ConfirmReveal.Visibility == Visibility.Visible && ConfirmPasswordBox.Password != ConfirmReveal.Text)
                ConfirmPasswordBox.Password = ConfirmReveal.Text;
            UpdatePlaceholders();
            ValidateConfirmPassword();
            UpdateButtonState();
        }

        private void ConfirmEye_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmReveal.Visibility == Visibility.Visible)
            {
                ConfirmPasswordBox.Password = ConfirmReveal.Text;
                ConfirmReveal.Visibility = Visibility.Collapsed;
                ConfirmPasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                ConfirmReveal.Text = ConfirmPasswordBox.Password;
                ConfirmReveal.Visibility = Visibility.Visible;
                ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            }
            UpdatePlaceholders();
        }

        private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

        private void UpdatePassword_Click(object sender, RoutedEventArgs e) => UpdatePasswordRequested?.Invoke(this, EventArgs.Empty);
    }
}
