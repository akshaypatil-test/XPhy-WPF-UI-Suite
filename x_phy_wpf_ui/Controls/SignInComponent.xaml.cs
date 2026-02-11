using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class SignInComponent : UserControl
    {
        public event EventHandler NavigateToCreateAccount;
        public event EventHandler<SignInSuccessfulEventArgs>? SignInSuccessful;
        public event EventHandler ShowLoaderRequested;
        public event EventHandler<x_phy_wpf_ui.SignInFailedEventArgs> SignInFailed;
        public event EventHandler NavigateBack;
        public event EventHandler NavigateToRecoverUsername;
        public event EventHandler NavigateToForgotPassword;

        private readonly AuthService _authService;
        private readonly TokenStorage _tokenStorage;

        private bool _isEmailValid = false;
        private bool _isPasswordValid = false;

        public SignInComponent()
        {
            InitializeComponent();
            _authService = new AuthService();
            _tokenStorage = new TokenStorage();
            UpdateSignInButtonState();
            IsVisibleChanged += SignInComponent_IsVisibleChanged;
            Loaded += SignInComponent_Loaded;
        }

        private void SignInComponent_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateUsernamePlaceholder();
            UpdatePasswordPlaceholder();
        }

        private void SignInComponent_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible && e.NewValue is bool visible && visible)
            {
                ResetSignInButtonState();
            }
        }

        private void ResetSignInButtonState()
        {
            SignInButton.IsEnabled = _isEmailValid && _isPasswordValid;
        }

        /// <summary>Display an error message (e.g. after failed sign-in when returning from loader).</summary>
        public void SetError(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            ErrorMessageText.Text = message;
            ErrorMessageText.Visibility = Visibility.Visible;
        }

        /// <summary>Clear all inputs and errors when navigating back to this screen.</summary>
        public void ClearInputs()
        {
            UsernameTextBox.Text = "";
            PasswordBox.Password = "";
            PasswordRevealTextBox.Text = "";
            PasswordRevealTextBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            ErrorMessageText.Text = "";
            ErrorMessageText.Visibility = Visibility.Collapsed;
            EmailErrorText.Text = "";
            EmailErrorText.Visibility = Visibility.Collapsed;
            PasswordErrorText.Text = "";
            PasswordErrorText.Visibility = Visibility.Collapsed;
            _isEmailValid = false;
            _isPasswordValid = false;
            UpdateUsernamePlaceholder();
            UpdatePasswordPlaceholder();
            UpdateSignInButtonState();
            if (UsernameFieldBorder != null) UsernameFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorder");
            if (PasswordFieldBorder != null) PasswordFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorder");
        }

        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateUsernamePlaceholder();
            ValidateEmail();
            UpdateSignInButtonState();
        }

        private void UpdateUsernamePlaceholder()
        {
            UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UsernameTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UsernameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateEmail();
            UpdateSignInButtonState();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (PasswordRevealTextBox.Visibility != Visibility.Visible)
                UpdatePasswordPlaceholder();
            ValidatePassword();
            UpdateSignInButtonState();
        }

        private void PasswordRevealTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PasswordRevealTextBox.Visibility == Visibility.Visible)
            {
                var text = PasswordRevealTextBox.Text;
                if (PasswordBox.Password != text)
                    PasswordBox.Password = text;
                UpdatePasswordPlaceholder();
            }
            ValidatePassword();
            UpdateSignInButtonState();
        }

        private void PasswordEyeButton_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordRevealTextBox.Visibility == Visibility.Visible)
            {
                PasswordBox.Password = PasswordRevealTextBox.Text;
                PasswordRevealTextBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                PasswordRevealTextBox.Text = PasswordBox.Password;
                PasswordRevealTextBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
            }
            UpdatePasswordPlaceholder();
        }

        private void UpdatePasswordPlaceholder()
        {
            var hasText = PasswordRevealTextBox.Visibility == Visibility.Visible
                ? !string.IsNullOrEmpty(PasswordRevealTextBox.Text)
                : !string.IsNullOrEmpty(PasswordBox.Password);
            PasswordPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (PasswordFieldBorder != null)
                PasswordFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorderFocused");
        }

        private void PasswordRevealTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (PasswordFieldBorder != null)
                PasswordFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorderFocused");
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidatePassword();
            UpdateSignInButtonState();
        }

        private void ValidateEmail()
        {
            var email = UsernameTextBox.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(email))
            {
                _isEmailValid = false;
                EmailErrorText.Text = "Email is required";
                EmailErrorText.Visibility = Visibility.Visible;
                SetTextBoxErrorState(UsernameTextBox, true);
            }
            else if (!IsValidEmail(email))
            {
                _isEmailValid = false;
                EmailErrorText.Text = "Please enter a valid email address";
                EmailErrorText.Visibility = Visibility.Visible;
                SetTextBoxErrorState(UsernameTextBox, true);
            }
            else
            {
                _isEmailValid = true;
                EmailErrorText.Visibility = Visibility.Collapsed;
                SetTextBoxErrorState(UsernameTextBox, false);
            }
        }

        private void ValidatePassword()
        {
            var password = PasswordBox.Password;
            
            if (string.IsNullOrWhiteSpace(password))
            {
                _isPasswordValid = false;
                PasswordErrorText.Text = "Password is required";
                PasswordErrorText.Visibility = Visibility.Visible;
                SetPasswordBoxErrorState(PasswordBox, true);
            }
            else
            {
                _isPasswordValid = true;
                PasswordErrorText.Visibility = Visibility.Collapsed;
                SetPasswordBoxErrorState(PasswordBox, false);
            }
        }

        private void SetTextBoxErrorState(TextBox textBox, bool hasError)
        {
            if (UsernameFieldBorder == null) return;
            if (hasError)
            {
                UsernameFieldBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 107));
                UsernameFieldBorder.BorderThickness = new Thickness(1);
            }
            else
            {
                UsernameFieldBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
                UsernameFieldBorder.BorderThickness = new Thickness(1);
            }
        }

        private void UsernameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UsernameFieldBorder != null)
                UsernameFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorderFocused");
        }

        private void SetPasswordBoxErrorState(PasswordBox passwordBox, bool hasError)
        {
            if (PasswordFieldBorder == null) return;
            if (hasError)
            {
                PasswordFieldBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 107));
                PasswordFieldBorder.BorderThickness = new Thickness(1);
            }
            else
            {
                PasswordFieldBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
                PasswordFieldBorder.BorderThickness = new Thickness(1);
            }
        }

        private void UpdateSignInButtonState()
        {
            SignInButton.IsEnabled = _isEmailValid && _isPasswordValid;
        }

        private async void SignIn_Click(object sender, RoutedEventArgs e)
        {
            // Clear previous error
            ErrorMessageText.Visibility = Visibility.Collapsed;
            ErrorMessageText.Text = "";

            // Get input values
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;

            // Validate inputs
            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Please enter your email address.");
                return;
            }

            if (!IsValidEmail(username))
            {
                ShowError("Please enter a valid email address.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter your password.");
                return;
            }

            // Show loader screen (no button loader); parent will show LoaderComponent
            ShowLoaderRequested?.Invoke(this, EventArgs.Empty);

            bool rememberMe = RememberMeCheckBox?.IsChecked == true;

            try
            {
                var response = await _authService.LoginAsync(username, password, null, rememberMe);

                if (response != null && response.User != null)
                {
                    // Do NOT save tokens yet. Parent will run native Keygen validation first; only if valid will it save tokens and complete login.
                    var licenseInfo = response.License;
                    if (licenseInfo == null && response.User != null)
                    {
                        licenseInfo = new LicenseInfo
                        {
                            Status = string.IsNullOrEmpty(response.User.LicenseStatus) ? "Trial" : response.User.LicenseStatus,
                            TrialEndsAt = response.User.TrialEndsAt
                        };
                    }
                    var licenseKey = response.License?.Key ?? licenseInfo?.Key;
                    SignInSuccessful?.Invoke(this, new SignInSuccessfulEventArgs(licenseKey, response, false, rememberMe));
                }
                else
                {
                    SignInFailed?.Invoke(this, new x_phy_wpf_ui.SignInFailedEventArgs("Login failed. Please check your credentials or contact support."));
                }
            }
            catch (Exception ex)
            {
                SignInFailed?.Invoke(this, new x_phy_wpf_ui.SignInFailedEventArgs(ex.Message));
            }
        }

        private void CreateAccount_Click(object sender, RoutedEventArgs e)
        {
            NavigateToCreateAccount?.Invoke(this, EventArgs.Empty);
        }

        private void ForgotUsername_Click(object sender, RoutedEventArgs e)
        {
            NavigateToRecoverUsername?.Invoke(this, EventArgs.Empty);
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            NavigateToForgotPassword?.Invoke(this, EventArgs.Empty);
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            NavigateBack?.Invoke(this, EventArgs.Empty);
        }

        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorMessageText.Visibility = Visibility.Visible;
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
                return regex.IsMatch(email);
            }
            catch
            {
                return false;
            }
        }
    }
}
