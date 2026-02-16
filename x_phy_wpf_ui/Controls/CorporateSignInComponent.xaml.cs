using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class CorporateSignInComponent : UserControl
    {
        public event EventHandler NavigateBack;
        public event EventHandler NavigateToRecoverUsername;
        public event EventHandler NavigateToForgotPassword;
        public event EventHandler SignInSuccessful;
        public event EventHandler<bool> SignInRequiresPasswordChange; // true = show Update Password screen
        public event EventHandler<x_phy_wpf_ui.SignInFailedEventArgs> SignInFailed;
        public event EventHandler ShowLoaderRequested;

        private readonly AuthService _authService;
        private readonly TokenStorage _tokenStorage;
        private bool _isEmailValid;
        private bool _isPasswordValid;

        public CorporateSignInComponent()
        {
            InitializeComponent();
            _authService = new AuthService();
            _tokenStorage = new TokenStorage();
            UpdateSignInButtonState();
            Loaded += (s, e) =>
            {
                UpdateUsernamePlaceholder();
                UpdatePasswordPlaceholder();
                UpdateLicenseKeyPlaceholder();
            };
        }

        public void ClearInputs()
        {
            UsernameTextBox.Text = "";
            PasswordBox.Password = "";
            PasswordRevealTextBox.Text = "";
            PasswordRevealTextBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            LicenseKeyTextBox.Text = "";
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
            UpdateLicenseKeyPlaceholder();
            UpdateSignInButtonState();
            if (UsernameFieldBorder != null) UsernameFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Border");
            if (PasswordFieldBorder != null) PasswordFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Border");
            if (LicenseKeyFieldBorder != null) LicenseKeyFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Border");
        }

        public void SetError(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            ErrorMessageText.Text = message;
            ErrorMessageText.Visibility = Visibility.Visible;
        }

        private void UpdateUsernamePlaceholder()
        {
            if (UsernamePlaceholder != null)
                UsernamePlaceholder.Visibility = string.IsNullOrEmpty(UsernameTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePasswordPlaceholder()
        {
            var hasText = PasswordRevealTextBox?.Visibility == Visibility.Visible
                ? !string.IsNullOrEmpty(PasswordRevealTextBox.Text)
                : !string.IsNullOrEmpty(PasswordBox?.Password);
            if (PasswordPlaceholder != null)
                PasswordPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateLicenseKeyPlaceholder()
        {
            if (LicenseKeyPlaceholder != null)
                LicenseKeyPlaceholder.Visibility = string.IsNullOrEmpty(LicenseKeyTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateUsernamePlaceholder();
            ValidateEmail();
            UpdateSignInButtonState();
        }

        private void UsernameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UsernameFieldBorder != null)
                UsernameFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Primary");
        }

        private void UsernameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateEmail();
            UpdateSignInButtonState();
        }

        private void LicenseKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateLicenseKeyPlaceholder();
        }

        private void LicenseKeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (LicenseKeyFieldBorder != null)
                LicenseKeyFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Primary");
        }

        private void LicenseKeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (LicenseKeyFieldBorder != null)
                LicenseKeyFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Border");
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (PasswordRevealTextBox?.Visibility != Visibility.Visible)
                UpdatePasswordPlaceholder();
            ValidatePassword();
            UpdateSignInButtonState();
        }

        private void PasswordRevealTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PasswordRevealTextBox?.Visibility == Visibility.Visible)
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

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (PasswordFieldBorder != null)
                PasswordFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Primary");
        }

        private void PasswordRevealTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (PasswordFieldBorder != null)
                PasswordFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Primary");
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidatePassword();
            UpdateSignInButtonState();
        }

        private void ValidateEmail()
        {
            var email = UsernameTextBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(email))
            {
                _isEmailValid = false;
                EmailErrorText.Text = "Email is required";
                EmailErrorText.Visibility = Visibility.Visible;
                SetUsernameBorderError(true);
            }
            else if (!IsValidEmail(email))
            {
                _isEmailValid = false;
                EmailErrorText.Text = "Please enter a valid email address";
                EmailErrorText.Visibility = Visibility.Visible;
                SetUsernameBorderError(true);
            }
            else
            {
                _isEmailValid = true;
                EmailErrorText.Visibility = Visibility.Collapsed;
                SetUsernameBorderError(false);
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
                SetPasswordBorderError(true);
            }
            else
            {
                _isPasswordValid = true;
                PasswordErrorText.Visibility = Visibility.Collapsed;
                SetPasswordBorderError(false);
            }
        }

        private void SetUsernameBorderError(bool hasError)
        {
            if (UsernameFieldBorder == null) return;
            if (hasError)
                UsernameFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Error");
            else
                UsernameFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Border");
        }

        private void SetPasswordBorderError(bool hasError)
        {
            if (PasswordFieldBorder == null) return;
            if (hasError)
                PasswordFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Error");
            else
                PasswordFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Border");
        }

        private void UpdateSignInButtonState()
        {
            SignInButton.IsEnabled = _isEmailValid && _isPasswordValid;
        }

        private async void SignIn_Click(object sender, RoutedEventArgs e)
        {
            ErrorMessageText.Visibility = Visibility.Collapsed;
            ErrorMessageText.Text = "";

            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;
            var licenseKey = LicenseKeyTextBox?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                SetError("Please enter your email address.");
                return;
            }
            if (!IsValidEmail(username))
            {
                SetError("Please enter a valid email address.");
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                SetError("Please enter your password.");
                return;
            }

            bool rememberMe = RememberMeCheckBox?.IsChecked == true;
            ShowLoaderRequested?.Invoke(this, EventArgs.Empty);

            try
            {
                var response = await _authService.LoginAsync(username, password, licenseKey, rememberMe);

                if (response != null && response.User != null)
                {
                    // Use license key from input for config.toml and native Keygen validation (same as normal sign-in).
                    var keyForConfig = string.IsNullOrWhiteSpace(licenseKey) ? null : licenseKey.Trim();
                    if (response.FirstTimeLogin)
                    {
                        // Save tokens so update-password flow has a session; license key stored for validation after password update.
                        var licenseInfo = response.License;
                        if (licenseInfo == null)
                        {
                            licenseInfo = new LicenseInfo
                            {
                                Status = string.IsNullOrEmpty(response.User.LicenseStatus) ? "Trial" : response.User.LicenseStatus,
                                TrialEndsAt = response.User.TrialEndsAt,
                                Key = keyForConfig
                            };
                        }
                        else if (!string.IsNullOrWhiteSpace(keyForConfig))
                        {
                            licenseInfo = new LicenseInfo
                            {
                                Key = keyForConfig,
                                Status = licenseInfo.Status,
                                MaxDevices = licenseInfo.MaxDevices,
                                PlanId = licenseInfo.PlanId,
                                PlanName = licenseInfo.PlanName,
                                TrialEndsAt = licenseInfo.TrialEndsAt,
                                PurchaseDate = licenseInfo.PurchaseDate,
                                ExpiryDate = licenseInfo.ExpiryDate,
                                TrialAttemptsRemaining = licenseInfo.TrialAttemptsRemaining
                            };
                        }
                        _tokenStorage.SaveTokens(
                            response.AccessToken,
                            response.RefreshToken,
                            response.ExpiresIn,
                            response.User.Id,
                            response.User.Username,
                            response.User,
                            licenseInfo,
                            rememberMe
                        );
                        SignInRequiresPasswordChange?.Invoke(this, true);
                    }
                    else
                    {
                        // Same flow as normal sign-in: do not save tokens here; MainWindow will write config, run Keygen, then save and show app.
                        SignInSuccessful?.Invoke(this, new x_phy_wpf_ui.SignInSuccessfulEventArgs(keyForConfig, response, fromCorporateSignIn: true, rememberMe));
                    }
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

        private void ForgotUsername_Click(object sender, RoutedEventArgs e) => NavigateToRecoverUsername?.Invoke(this, EventArgs.Empty);
        private void ForgotPassword_Click(object sender, RoutedEventArgs e) => NavigateToForgotPassword?.Invoke(this, EventArgs.Empty);
        private void Back_Click(object sender, RoutedEventArgs e) => NavigateBack?.Invoke(this, EventArgs.Empty);

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                return new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase).IsMatch(email);
            }
            catch { return false; }
        }
    }
}
