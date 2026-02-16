using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class ForgotPasswordComponent : UserControl
    {
        public event EventHandler NavigateBack;
        public event EventHandler<string> NavigateToVerifyOtp;

        private readonly AuthService _authService;
        private const int MaxAttempts = 3;

        public ForgotPasswordComponent()
        {
            InitializeComponent();
            _authService = new AuthService();
            AttemptsText.Text = $"Attempts Left: {MaxAttempts}/{MaxAttempts}";
            Loaded += (s, e) => UpdateEmailPlaceholder();
            IsVisibleChanged += (s, e) =>
            {
                if (e.NewValue is true)
                    SendOtpButton.IsEnabled = true;
            };
        }

        /// <summary>Clear all inputs and errors when navigating back to this screen.</summary>
        public void ClearInputs()
        {
            EmailTextBox.Text = "";
            ErrorMessageText.Text = "";
            ErrorMessageText.Visibility = Visibility.Collapsed;
            EmailErrorText.Visibility = Visibility.Collapsed;
            UpdateEmailPlaceholder();
            SetEmailFieldError(false);
        }

        private void UpdateEmailPlaceholder()
        {
            if (EmailPlaceholder != null)
                EmailPlaceholder.Visibility = string.IsNullOrEmpty(EmailTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateEmailPlaceholder();
        }

        private void EmailTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (EmailFieldBorder != null)
                EmailFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Primary");
        }

        private void EmailTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var email = EmailTextBox?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(email) && IsValidEmail(email))
            {
                EmailErrorText.Visibility = Visibility.Collapsed;
                SetEmailFieldError(false);
            }
            else if (EmailFieldBorder != null && EmailErrorText.Visibility != Visibility.Visible)
            {
                EmailFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Border");
            }
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

        private void UpdateAttemptsDisplay(int attemptsRemaining)
        {
            AttemptsText.Text = $"Attempts Left: {attemptsRemaining}/{MaxAttempts}";
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

        private async void SendOtp_Click(object sender, RoutedEventArgs e)
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

            SendOtpButton.IsEnabled = false;
            try
            {
                var response = await _authService.ForgotPasswordAsync(email);
                UpdateAttemptsDisplay(response.AttemptsRemaining);

                if (response.AccountFound == false)
                {
                    ErrorMessageText.Text = string.IsNullOrWhiteSpace(response.Message)
                        ? "No account found with this email address. Please check the email or create an account."
                        : response.Message;
                    ErrorMessageText.Visibility = Visibility.Visible;
                    SetEmailFieldError(true);
                    SendOtpButton.IsEnabled = true;
                    return;
                }

                if (response.AttemptsRemaining == 0)
                {
                    ErrorMessageText.Text = "You have used all recovery attempts for this email. Please try again after 24 hours.";
                    ErrorMessageText.Visibility = Visibility.Visible;
                    SendOtpButton.IsEnabled = false;
                }
                else
                {
                    NavigateToVerifyOtp?.Invoke(this, email);
                }
            }
            catch (Exception ex)
            {
                ErrorMessageText.Text = ex.Message;
                ErrorMessageText.Visibility = Visibility.Visible;
                SendOtpButton.IsEnabled = true;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigateBack?.Invoke(this, EventArgs.Empty);
        }
    }
}
