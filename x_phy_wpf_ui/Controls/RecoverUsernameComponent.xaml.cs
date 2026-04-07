using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class RecoverUsernameComponent : UserControl
    {
        public event EventHandler NavigateBack;
        public event EventHandler NavigateToSuccess;

        private readonly AuthService _authService;
        private const int MaxAttempts = 3;
        private bool _isEmailValid;

        public RecoverUsernameComponent()
        {
            InitializeComponent();
            _authService = new AuthService();
            _isEmailValid = false;
            AttemptsCountRun.Text = $"{MaxAttempts}/{MaxAttempts}";
            SendUsernameButton.IsEnabled = false;
            Loaded += (s, e) => UpdateEmailPlaceholder();
        }

        /// <summary>Clear all inputs and errors when navigating back to this screen. Resets attempt count display to full (3/3).</summary>
        public void ClearInputs()
        {
            EmailTextBox.Text = "";
            ErrorMessageText.Text = "";
            ErrorMessageText.Visibility = Visibility.Collapsed;
            EmailErrorText.Visibility = Visibility.Collapsed;
            _isEmailValid = false;
            UpdateEmailPlaceholder();
            UpdateAttemptsDisplay(MaxAttempts);
            UpdateSendUsernameButtonState();
            SetEmailFieldError(false);
            if (EmailFieldBorder != null)
                EmailFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Border");
        }

        private void UpdateEmailPlaceholder()
        {
            if (EmailPlaceholder != null)
                EmailPlaceholder.Visibility = string.IsNullOrEmpty(EmailTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateEmailPlaceholder();
            ValidateEmail();
            UpdateSendUsernameButtonState();
        }

        private void EmailTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (EmailFieldBorder != null)
                EmailFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Primary");
        }

        private void EmailTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateEmail();
            UpdateSendUsernameButtonState();
        }

        private void ValidateEmail()
        {
            var email = EmailTextBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(email))
            {
                _isEmailValid = false;
                EmailErrorText.Text = "Email is required";
                EmailErrorText.Visibility = Visibility.Visible;
                SetEmailFieldError(true);
            }
            else if (!IsValidEmail(email))
            {
                _isEmailValid = false;
                EmailErrorText.Text = "Please enter a valid email address";
                EmailErrorText.Visibility = Visibility.Visible;
                SetEmailFieldError(true);
            }
            else
            {
                _isEmailValid = true;
                EmailErrorText.Visibility = Visibility.Collapsed;
                SetEmailFieldError(false);
            }
        }

        private void UpdateSendUsernameButtonState()
        {
            SendUsernameButton.IsEnabled = _isEmailValid;
        }

        private void UpdateAttemptsDisplay(int attemptsRemaining)
        {
            AttemptsCountRun.Text = $"{attemptsRemaining}/{MaxAttempts}";
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                return new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase).IsMatch(email);
            }
            catch { return false; }
        }

        private async void SendUsername_Click(object sender, RoutedEventArgs e)
        {
            ErrorMessageText.Visibility = Visibility.Collapsed;
            ErrorMessageText.Text = "";
            var email = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                EmailErrorText.Text = "Email is required";
                EmailErrorText.Visibility = Visibility.Visible;
                SetEmailFieldError(true);
                return;
            }
            if (!IsValidEmail(email))
            {
                EmailErrorText.Text = "Please enter a valid email address";
                EmailErrorText.Visibility = Visibility.Visible;
                SetEmailFieldError(true);
                return;
            }
            EmailErrorText.Visibility = Visibility.Collapsed;
            SetEmailFieldError(false);

            SendUsernameButton.IsEnabled = false;
            try
            {
                var response = await _authService.ForgotUsernameAsync(email);
                UpdateAttemptsDisplay(response.AttemptsRemaining);

                if (response.AccountFound == false)
                {
                    ErrorMessageText.Text = string.IsNullOrWhiteSpace(response.Message)
                        ? "No account found with this email address. Please check the email or create an account."
                        : response.Message;
                    ErrorMessageText.Visibility = Visibility.Visible;
                    SetEmailFieldError(true);
                    UpdateSendUsernameButtonState();
                    return;
                }

                if (response.AttemptsRemaining == 0)
                {
                    ErrorMessageText.Text = "You have used all recovery attempts for this email. Please try again after 24 hours.";
                    ErrorMessageText.Visibility = Visibility.Visible;
                    SendUsernameButton.IsEnabled = false;
                }
                else
                {
                    NavigateToSuccess?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                ErrorMessageText.Text = ex.Message;
                ErrorMessageText.Visibility = Visibility.Visible;
                UpdateSendUsernameButtonState();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigateBack?.Invoke(this, EventArgs.Empty);
        }

        private void SetEmailFieldError(bool hasError)
        {
            if (EmailFieldBorder == null) return;
            if (hasError)
            {
                EmailFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Error");
                EmailFieldBorder.BorderThickness = new Thickness(1);
            }
            else
            {
                EmailFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Border");
                EmailFieldBorder.BorderThickness = new Thickness(1);
            }
        }
    }
}
