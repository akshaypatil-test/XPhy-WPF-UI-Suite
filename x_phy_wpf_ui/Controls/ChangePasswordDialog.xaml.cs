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

        private void CurrentPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (CurrentReveal.Visibility != Visibility.Visible)
                UpdatePlaceholders();
            UpdateButtonState();
        }

        private void CurrentReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CurrentReveal.Visibility == Visibility.Visible && CurrentPasswordBox.Password != CurrentReveal.Text)
                CurrentPasswordBox.Password = CurrentReveal.Text;
            UpdatePlaceholders();
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
            UpdateButtonState();
        }

        private void NewReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (NewReveal.Visibility == Visibility.Visible && NewPasswordBox.Password != NewReveal.Text)
                NewPasswordBox.Password = NewReveal.Text;
            UpdatePlaceholders();
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
            UpdateButtonState();
        }

        private void ConfirmReveal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ConfirmReveal.Visibility == Visibility.Visible && ConfirmPasswordBox.Password != ConfirmReveal.Text)
                ConfirmPasswordBox.Password = ConfirmReveal.Text;
            UpdatePlaceholders();
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
