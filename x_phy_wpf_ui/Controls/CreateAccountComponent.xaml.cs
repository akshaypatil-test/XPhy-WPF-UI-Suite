using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class CreateAccountComponent : UserControl
    {
        public event EventHandler NavigateToSignIn;
        public event EventHandler AccountCreated;
        public event EventHandler NavigateBack;
        public event EventHandler<EmailVerificationEventArgs> NavigateToEmailVerification;

        private readonly AuthService _authService;
        private bool _isFirstNameValid = false;
        private bool _isLastNameValid = false;
        private bool _isEmailValid = false;
        private bool _isPasswordValid = false;
        private bool _isConfirmPasswordValid = false;

        public CreateAccountComponent()
        {
            InitializeComponent();
            _authService = new AuthService();
            UpdateSignUpButtonState();
            Loaded += CreateAccountComponent_Loaded;
        }

        private void CreateAccountComponent_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateFirstNamePlaceholder();
            UpdateLastNamePlaceholder();
            UpdateEmailPlaceholder();
            UpdatePasswordPlaceholder();
            UpdateConfirmPasswordPlaceholder();
        }

        /// <summary>Clear all inputs and errors when navigating back to this screen.</summary>
        public void ClearInputs()
        {
            FirstNameTextBox.Text = "";
            LastNameTextBox.Text = "";
            EmailTextBox.Text = "";
            PasswordBox.Password = "";
            PasswordRevealTextBox.Text = "";
            PasswordRevealTextBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            ConfirmPasswordBox.Password = "";
            ConfirmPasswordRevealTextBox.Text = "";
            ConfirmPasswordRevealTextBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            ErrorMessageText.Text = "";
            ErrorMessageText.Visibility = Visibility.Collapsed;
            FirstNameErrorText.Text = "";
            FirstNameErrorText.Visibility = Visibility.Collapsed;
            LastNameErrorText.Text = "";
            LastNameErrorText.Visibility = Visibility.Collapsed;
            EmailErrorText.Text = "";
            EmailErrorText.Visibility = Visibility.Collapsed;
            PasswordErrorText.Text = "";
            PasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Text = "";
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
            _isFirstNameValid = false;
            _isLastNameValid = false;
            _isEmailValid = false;
            _isPasswordValid = false;
            _isConfirmPasswordValid = false;
            UpdateFirstNamePlaceholder();
            UpdateLastNamePlaceholder();
            UpdateEmailPlaceholder();
            UpdatePasswordPlaceholder();
            UpdateConfirmPasswordPlaceholder();
            UpdateSignUpButtonState();
            SignUpButton.Content = "Continue Setup";
            SetTextBoxErrorState(null, false, FirstNameFieldBorder);
            SetTextBoxErrorState(null, false, LastNameFieldBorder);
            SetTextBoxErrorState(null, false, EmailFieldBorder);
            SetPasswordBoxErrorState(PasswordBox, false);
            SetPasswordBoxErrorState(ConfirmPasswordBox, false);
        }

        private void FirstNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateFirstNamePlaceholder();
            ValidateFirstName();
            UpdateSignUpButtonState();
        }

        private void UpdateFirstNamePlaceholder()
        {
            if (FirstNamePlaceholder != null)
                FirstNamePlaceholder.Visibility = string.IsNullOrEmpty(FirstNameTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LastNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateLastNamePlaceholder();
            ValidateLastName();
            UpdateSignUpButtonState();
        }

        private void UpdateLastNamePlaceholder()
        {
            if (LastNamePlaceholder != null)
                LastNamePlaceholder.Visibility = string.IsNullOrEmpty(LastNameTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateEmailPlaceholder();
            ValidateEmail();
            UpdateSignUpButtonState();
        }

        private void UpdateEmailPlaceholder()
        {
            if (EmailPlaceholder != null)
                EmailPlaceholder.Visibility = string.IsNullOrEmpty(EmailTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EmailTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateEmail();
            UpdateSignUpButtonState();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (PasswordRevealTextBox?.Visibility != Visibility.Visible)
                UpdatePasswordPlaceholder();
            ValidatePassword();
            ValidateConfirmPassword();
            UpdateSignUpButtonState();
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
            ValidateConfirmPassword();
            UpdateSignUpButtonState();
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
            if (PasswordPlaceholder == null) return;
            var hasText = PasswordRevealTextBox.Visibility == Visibility.Visible
                ? !string.IsNullOrEmpty(PasswordRevealTextBox.Text)
                : !string.IsNullOrEmpty(PasswordBox.Password);
            PasswordPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidatePassword();
            ValidateConfirmPassword();
            UpdateSignUpButtonState();
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ConfirmPasswordRevealTextBox?.Visibility != Visibility.Visible)
                UpdateConfirmPasswordPlaceholder();
            ValidateConfirmPassword();
            UpdateSignUpButtonState();
        }

        private void ConfirmPasswordRevealTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible)
            {
                var text = ConfirmPasswordRevealTextBox.Text;
                if (ConfirmPasswordBox.Password != text)
                    ConfirmPasswordBox.Password = text;
                UpdateConfirmPasswordPlaceholder();
            }
            ValidateConfirmPassword();
            UpdateSignUpButtonState();
        }

        private void ConfirmPasswordEyeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible)
            {
                ConfirmPasswordBox.Password = ConfirmPasswordRevealTextBox.Text;
                ConfirmPasswordRevealTextBox.Visibility = Visibility.Collapsed;
                ConfirmPasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                ConfirmPasswordRevealTextBox.Text = ConfirmPasswordBox.Password;
                ConfirmPasswordRevealTextBox.Visibility = Visibility.Visible;
                ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            }
            UpdateConfirmPasswordPlaceholder();
        }

        private void UpdateConfirmPasswordPlaceholder()
        {
            if (ConfirmPasswordPlaceholder == null) return;
            var hasText = ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible
                ? !string.IsNullOrEmpty(ConfirmPasswordRevealTextBox.Text)
                : !string.IsNullOrEmpty(ConfirmPasswordBox.Password);
            ConfirmPasswordPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ConfirmPasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ValidateConfirmPassword();
            UpdateSignUpButtonState();
        }

        private void ValidateFirstName()
        {
            var first = FirstNameTextBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(first))
            {
                _isFirstNameValid = false;
                if (FirstNameErrorText != null)
                {
                    FirstNameErrorText.Text = "First name is required";
                    FirstNameErrorText.Visibility = Visibility.Visible;
                }
                SetTextBoxErrorState(FirstNameTextBox, true, FirstNameFieldBorder);
            }
            else if (first.Length > 100)
            {
                _isFirstNameValid = false;
                if (FirstNameErrorText != null)
                {
                    FirstNameErrorText.Text = "First name must be 100 characters or less";
                    FirstNameErrorText.Visibility = Visibility.Visible;
                }
                SetTextBoxErrorState(FirstNameTextBox, true, FirstNameFieldBorder);
            }
            else
            {
                _isFirstNameValid = true;
                if (FirstNameErrorText != null)
                    FirstNameErrorText.Visibility = Visibility.Collapsed;
                SetTextBoxErrorState(FirstNameTextBox, false, FirstNameFieldBorder);
            }
        }

        private void ValidateLastName()
        {
            var last = LastNameTextBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(last))
            {
                _isLastNameValid = false;
                if (LastNameErrorText != null)
                {
                    LastNameErrorText.Text = "Last name is required";
                    LastNameErrorText.Visibility = Visibility.Visible;
                }
                SetTextBoxErrorState(LastNameTextBox, true, LastNameFieldBorder);
            }
            else if (last.Length > 100)
            {
                _isLastNameValid = false;
                if (LastNameErrorText != null)
                {
                    LastNameErrorText.Text = "Last name must be 100 characters or less";
                    LastNameErrorText.Visibility = Visibility.Visible;
                }
                SetTextBoxErrorState(LastNameTextBox, true, LastNameFieldBorder);
            }
            else
            {
                _isLastNameValid = true;
                if (LastNameErrorText != null)
                    LastNameErrorText.Visibility = Visibility.Collapsed;
                SetTextBoxErrorState(LastNameTextBox, false, LastNameFieldBorder);
            }
        }

        private void ValidateEmail()
        {
            var email = EmailTextBox.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(email))
            {
                _isEmailValid = false;
                EmailErrorText.Text = "Email is required";
                EmailErrorText.Visibility = Visibility.Visible;
                SetTextBoxErrorState(EmailTextBox, true);
            }
            else if (!IsValidEmail(email))
            {
                _isEmailValid = false;
                EmailErrorText.Text = "Please enter a valid email address";
                EmailErrorText.Visibility = Visibility.Visible;
                SetTextBoxErrorState(EmailTextBox, true);
            }
            else
            {
                _isEmailValid = true;
                EmailErrorText.Visibility = Visibility.Collapsed;
                SetTextBoxErrorState(EmailTextBox, false);
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
            else if (password.Length < 8)
            {
                _isPasswordValid = false;
                PasswordErrorText.Text = "Password must be at least 8 characters long";
                PasswordErrorText.Visibility = Visibility.Visible;
                SetPasswordBoxErrorState(PasswordBox, true);
            }
            else if (!IsValidPassword(password))
            {
                _isPasswordValid = false;
                PasswordErrorText.Text = "Password must contain uppercase, lowercase, number, and special character (@$!%*?&)";
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

        private void ValidateConfirmPassword()
        {
            var password = PasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;
            
            if (string.IsNullOrWhiteSpace(confirmPassword))
            {
                _isConfirmPasswordValid = false;
                ConfirmPasswordErrorText.Text = "Please confirm your password";
                ConfirmPasswordErrorText.Visibility = Visibility.Visible;
                SetPasswordBoxErrorState(ConfirmPasswordBox, true);
            }
            else if (password != confirmPassword)
            {
                _isConfirmPasswordValid = false;
                ConfirmPasswordErrorText.Text = "Passwords do not match";
                ConfirmPasswordErrorText.Visibility = Visibility.Visible;
                SetPasswordBoxErrorState(ConfirmPasswordBox, true);
            }
            else
            {
                _isConfirmPasswordValid = true;
                ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
                SetPasswordBoxErrorState(ConfirmPasswordBox, false);
            }
        }

        private void SetTextBoxErrorState(TextBox textBox, bool hasError, System.Windows.Controls.Border border = null)
        {
            if (border == null)
            {
                if (textBox == FirstNameTextBox) border = FirstNameFieldBorder;
                else if (textBox == LastNameTextBox) border = LastNameFieldBorder;
                else if (textBox == EmailTextBox) border = EmailFieldBorder;
            }
            if (border == null) return;
            if (hasError)
            {
                border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 107));
                border.BorderThickness = new Thickness(1);
            }
            else
            {
                border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
                border.BorderThickness = new Thickness(1);
            }
        }

        private void FirstNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (FirstNameFieldBorder != null)
                FirstNameFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorderFocused");
        }

        private void FirstNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (FirstNameFieldBorder != null)
                FirstNameFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorder");
            ValidateFirstName();
            UpdateSignUpButtonState();
        }

        private void LastNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (LastNameFieldBorder != null)
                LastNameFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorderFocused");
        }

        private void LastNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (LastNameFieldBorder != null)
                LastNameFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorder");
            ValidateLastName();
            UpdateSignUpButtonState();
        }

        private void EmailTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (EmailFieldBorder != null)
                EmailFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorderFocused");
        }

        private void SetPasswordBoxErrorState(PasswordBox passwordBox, bool hasError)
        {
            var border = passwordBox == PasswordBox ? PasswordFieldBorder : ConfirmPasswordFieldBorder;
            if (border == null) return;
            if (hasError)
            {
                border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 107));
                border.BorderThickness = new Thickness(1);
            }
            else
            {
                border.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42));
                border.BorderThickness = new Thickness(1);
            }
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

        private void ConfirmPasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ConfirmPasswordFieldBorder != null)
                ConfirmPasswordFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorderFocused");
        }

        private void ConfirmPasswordRevealTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ConfirmPasswordFieldBorder != null)
                ConfirmPasswordFieldBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("InputBorderFocused");
        }

        private void UpdateSignUpButtonState()
        {
            SignUpButton.IsEnabled = _isFirstNameValid && _isLastNameValid && _isEmailValid && _isPasswordValid && _isConfirmPasswordValid;
        }

        private async void ContinueSetup_Click(object sender, RoutedEventArgs e)
        {
            // Clear previous error
            ErrorMessageText.Visibility = Visibility.Collapsed;
            ErrorMessageText.Text = "";

            // Get input values
            var firstName = FirstNameTextBox.Text.Trim();
            var lastName = LastNameTextBox.Text.Trim();
            var email = EmailTextBox.Text.Trim();
            var password = PasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            // Validate inputs
            if (string.IsNullOrWhiteSpace(firstName))
            {
                ShowError("Please enter your first name.");
                return;
            }

            if (string.IsNullOrWhiteSpace(lastName))
            {
                ShowError("Please enter your last name.");
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError("Please enter your email address.");
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowError("Please enter a valid email address.");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter a password.");
                return;
            }

            if (password.Length < 8)
            {
                ShowError("Password must be at least 8 characters long.");
                return;
            }

            if (!IsValidPassword(password))
            {
                ShowError("Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.");
                return;
            }

            if (password != confirmPassword)
            {
                ShowError("Passwords do not match.");
                return;
            }

            SignUpButton.Content = "Please wait...";
            SignUpButton.IsEnabled = false;

            try
            {
                var response = await _authService.RegisterAsync(email, password, firstName, lastName);

                if (response.RequiresEmailVerification)
                {
                    NavigateToEmailVerification?.Invoke(this, new EmailVerificationEventArgs { Email = email });
                    return;
                }

                // Legacy path if API does not require verification
                MessageBox.Show(
                    $"Account created successfully!\n\nYour 30-day free trial ends on: {response.TrialEndsAt:MMMM dd, yyyy}\n\nYou can now sign in with your credentials.",
                    "Registration Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                AccountCreated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                SignUpButton.Content = "Continue Setup";
                UpdateSignUpButtonState();
            }
        }

        private void SignIn_Click(object sender, RoutedEventArgs e)
        {
            NavigateToSignIn?.Invoke(this, EventArgs.Empty);
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

        private bool IsValidPassword(string password)
        {
            var hasUpper = new Regex(@"[A-Z]");
            var hasLower = new Regex(@"[a-z]");
            var hasNumber = new Regex(@"\d");
            var hasSpecial = new Regex(@"[@$!%*?&]");

            return hasUpper.IsMatch(password) &&
                   hasLower.IsMatch(password) &&
                   hasNumber.IsMatch(password) &&
                   hasSpecial.IsMatch(password);
        }
    }
}
