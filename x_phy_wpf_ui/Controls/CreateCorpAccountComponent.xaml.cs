using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class CreateCorpAccountComponent : UserControl
    {
        public event EventHandler BackRequested;
        public event EventHandler CorpAccountCreated;

        private readonly AuthService _authService;
        private bool _isFirstNameValid;
        private bool _isLastNameValid;
        private bool _isEmailValid;
        private bool _isPasswordValid;
        private bool _isConfirmPasswordValid;
        private bool _isMaxDevicesValid;
        private bool _isPolicyNumberValid;
        private bool _isOrganizationNameValid;
        private bool _isContactPersonNameValid;
        private bool _isCountryCodeValid;
        private bool _isContactNumberValid;
        private bool _isOrderNumberValid;
        private bool _isActivationDateValid;

        public CreateCorpAccountComponent()
        {
            InitializeComponent();
            _authService = new AuthService();
            Loaded += (s, e) =>
            {
                UpdatePlaceholders();
                UpdateCreateButtonState();
            };
            ActivationDatePicker.Loaded += ActivationDatePicker_Loaded;
        }

        private void ActivationDatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            // Style the DatePicker's inner text box so the selected date is visible (dark theme).
            // Run after template is applied (one layout pass later).
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var tb = FindVisualChild<TextBox>(ActivationDatePicker);
                if (tb != null)
                {
                    tb.Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                    tb.Foreground = new SolidColorBrush(Colors.White);
                    tb.CaretBrush = new SolidColorBrush(Colors.White);
                    tb.BorderThickness = new Thickness(0);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;
                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }

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
            MaxDevicesTextBox.Text = "";
            PolicyNumberTextBox.Text = "";
            OrganizationNameTextBox.Text = "";
            ContactPersonNameTextBox.Text = "";
            CountryCodeTextBox.Text = "";
            ContactNumberTextBox.Text = "";
            OrderNumberTextBox.Text = "";
            ActivationDatePicker.SelectedDate = null;
            ErrorMessageText.Text = "";
            ErrorMessageText.Visibility = Visibility.Collapsed;
            FirstNameErrorText.Visibility = Visibility.Collapsed;
            LastNameErrorText.Visibility = Visibility.Collapsed;
            EmailErrorText.Visibility = Visibility.Collapsed;
            PasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
            MaxDevicesErrorText.Visibility = Visibility.Collapsed;
            PolicyNumberErrorText.Visibility = Visibility.Collapsed;
            CountryCodeErrorText.Visibility = Visibility.Collapsed;
            ContactNumberErrorText.Visibility = Visibility.Collapsed;
            ActivationDateErrorText.Visibility = Visibility.Collapsed;
            _isFirstNameValid = _isLastNameValid = _isEmailValid = _isPasswordValid = _isConfirmPasswordValid = false;
            _isMaxDevicesValid = _isPolicyNumberValid = _isOrganizationNameValid = _isContactPersonNameValid = false;
            _isCountryCodeValid = _isContactNumberValid = _isOrderNumberValid = _isActivationDateValid = false;
            UpdatePlaceholders();
            UpdateCreateButtonState();
        }

        private void UpdatePlaceholders()
        {
            if (FirstNamePlaceholder != null) FirstNamePlaceholder.Visibility = string.IsNullOrEmpty(FirstNameTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (LastNamePlaceholder != null) LastNamePlaceholder.Visibility = string.IsNullOrEmpty(LastNameTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (EmailPlaceholder != null) EmailPlaceholder.Visibility = string.IsNullOrEmpty(EmailTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (PasswordPlaceholder != null) PasswordPlaceholder.Visibility = (PasswordRevealTextBox?.Visibility == Visibility.Visible ? !string.IsNullOrEmpty(PasswordRevealTextBox.Text) : !string.IsNullOrEmpty(PasswordBox?.Password)) ? Visibility.Collapsed : Visibility.Visible;
            if (ConfirmPasswordPlaceholder != null) ConfirmPasswordPlaceholder.Visibility = (ConfirmPasswordRevealTextBox?.Visibility == Visibility.Visible ? !string.IsNullOrEmpty(ConfirmPasswordRevealTextBox.Text) : !string.IsNullOrEmpty(ConfirmPasswordBox?.Password)) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void FirstNameTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateFirstName(); UpdateCreateButtonState(); }
        private void LastNameTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateLastName(); UpdateCreateButtonState(); }
        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateEmail(); UpdateCreateButtonState(); }
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) { if (PasswordRevealTextBox?.Visibility != Visibility.Visible) UpdatePlaceholders(); ValidatePassword(); ValidateConfirmPassword(); UpdateCreateButtonState(); }
        private void PasswordRevealTextBox_TextChanged(object sender, TextChangedEventArgs e) { if (PasswordRevealTextBox.Visibility == Visibility.Visible) { if (PasswordBox.Password != PasswordRevealTextBox.Text) PasswordBox.Password = PasswordRevealTextBox.Text; UpdatePlaceholders(); } ValidatePassword(); ValidateConfirmPassword(); UpdateCreateButtonState(); }
        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) { if (ConfirmPasswordRevealTextBox?.Visibility != Visibility.Visible) UpdatePlaceholders(); ValidateConfirmPassword(); UpdateCreateButtonState(); }
        private void ConfirmPasswordRevealTextBox_TextChanged(object sender, TextChangedEventArgs e) { if (ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible) { if (ConfirmPasswordBox.Password != ConfirmPasswordRevealTextBox.Text) ConfirmPasswordBox.Password = ConfirmPasswordRevealTextBox.Text; UpdatePlaceholders(); } ValidateConfirmPassword(); UpdateCreateButtonState(); }
        private void MaxDevicesTextBox_TextChanged(object sender, TextChangedEventArgs e) { ValidateMaxDevices(); UpdateCreateButtonState(); }
        private void PolicyNumberTextBox_TextChanged(object sender, TextChangedEventArgs e) { ValidatePolicyNumber(); UpdateCreateButtonState(); }
        private void OrganizationNameTextBox_TextChanged(object sender, TextChangedEventArgs e) { ValidateOrganizationName(); UpdateCreateButtonState(); }
        private void ContactPersonNameTextBox_TextChanged(object sender, TextChangedEventArgs e) { ValidateContactPersonName(); UpdateCreateButtonState(); }
        private void CountryCodeTextBox_TextChanged(object sender, TextChangedEventArgs e) { ValidateCountryCode(); UpdateCreateButtonState(); }
        private void ContactNumberTextBox_TextChanged(object sender, TextChangedEventArgs e) { ValidateContactNumber(); UpdateCreateButtonState(); }
        private void OrderNumberTextBox_TextChanged(object sender, TextChangedEventArgs e) { ValidateOrderNumber(); UpdateCreateButtonState(); }
        private void ActivationDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { ValidateActivationDate(); UpdateCreateButtonState(); }

        private void PasswordEyeButton_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordRevealTextBox.Visibility == Visibility.Visible) { PasswordBox.Password = PasswordRevealTextBox.Text; PasswordRevealTextBox.Visibility = Visibility.Collapsed; PasswordBox.Visibility = Visibility.Visible; }
            else { PasswordRevealTextBox.Text = PasswordBox.Password; PasswordRevealTextBox.Visibility = Visibility.Visible; PasswordBox.Visibility = Visibility.Collapsed; }
            UpdatePlaceholders();
        }

        private void ConfirmPasswordEyeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible) { ConfirmPasswordBox.Password = ConfirmPasswordRevealTextBox.Text; ConfirmPasswordRevealTextBox.Visibility = Visibility.Collapsed; ConfirmPasswordBox.Visibility = Visibility.Visible; }
            else { ConfirmPasswordRevealTextBox.Text = ConfirmPasswordBox.Password; ConfirmPasswordRevealTextBox.Visibility = Visibility.Visible; ConfirmPasswordBox.Visibility = Visibility.Collapsed; }
            UpdatePlaceholders();
        }

        private void ValidateFirstName() { var t = FirstNameTextBox?.Text?.Trim() ?? ""; _isFirstNameValid = t.Length >= 1; FirstNameErrorText.Visibility = _isFirstNameValid ? Visibility.Collapsed : Visibility.Visible; FirstNameErrorText.Text = _isFirstNameValid ? "" : "Required."; }
        private void ValidateLastName() { var t = LastNameTextBox?.Text?.Trim() ?? ""; _isLastNameValid = t.Length >= 1; LastNameErrorText.Visibility = _isLastNameValid ? Visibility.Collapsed : Visibility.Visible; LastNameErrorText.Text = _isLastNameValid ? "" : "Required."; }
        private void ValidateEmail() { var t = EmailTextBox?.Text?.Trim() ?? ""; _isEmailValid = IsValidEmail(t); EmailErrorText.Visibility = _isEmailValid ? Visibility.Collapsed : Visibility.Visible; EmailErrorText.Text = _isEmailValid ? "" : "Valid email required."; }
        private void ValidatePassword() { var p = PasswordRevealTextBox?.Visibility == Visibility.Visible ? PasswordRevealTextBox?.Text : PasswordBox?.Password; _isPasswordValid = !string.IsNullOrEmpty(p) && p.Length >= 8 && IsValidPassword(p); PasswordErrorText.Visibility = _isPasswordValid ? Visibility.Collapsed : Visibility.Visible; PasswordErrorText.Text = _isPasswordValid ? "" : "Min 8 chars, upper, lower, number, special."; }
        private void ValidateConfirmPassword() { var p = PasswordRevealTextBox?.Visibility == Visibility.Visible ? PasswordRevealTextBox?.Text : PasswordBox?.Password; var c = ConfirmPasswordRevealTextBox?.Visibility == Visibility.Visible ? ConfirmPasswordRevealTextBox?.Text : ConfirmPasswordBox?.Password; _isConfirmPasswordValid = !string.IsNullOrEmpty(c) && c == p; ConfirmPasswordErrorText.Visibility = _isConfirmPasswordValid ? Visibility.Collapsed : Visibility.Visible; ConfirmPasswordErrorText.Text = _isConfirmPasswordValid ? "" : "Passwords do not match."; }
        private void ValidateMaxDevices() { var t = MaxDevicesTextBox?.Text?.Trim() ?? ""; _isMaxDevicesValid = int.TryParse(t, out var n) && n > 0; MaxDevicesErrorText.Visibility = _isMaxDevicesValid ? Visibility.Collapsed : Visibility.Visible; MaxDevicesErrorText.Text = _isMaxDevicesValid ? "" : "Enter a positive number."; }
        private void ValidatePolicyNumber() { var t = PolicyNumberTextBox?.Text?.Trim() ?? ""; _isPolicyNumberValid = t.Length >= 1; PolicyNumberErrorText.Visibility = _isPolicyNumberValid ? Visibility.Collapsed : Visibility.Visible; PolicyNumberErrorText.Text = _isPolicyNumberValid ? "" : "Required."; }
        private void ValidateOrganizationName() { var t = OrganizationNameTextBox?.Text?.Trim() ?? ""; _isOrganizationNameValid = t.Length >= 1; }
        private void ValidateContactPersonName() { var t = ContactPersonNameTextBox?.Text?.Trim() ?? ""; _isContactPersonNameValid = t.Length >= 1; }
        private void ValidateCountryCode()
        {
            var t = CountryCodeTextBox?.Text?.Trim() ?? "";
            _isCountryCodeValid = t.Length >= 1;
            CountryCodeErrorText.Visibility = _isCountryCodeValid ? Visibility.Collapsed : Visibility.Visible;
            CountryCodeErrorText.Text = _isCountryCodeValid ? "" : "Required.";
        }

        private void ValidateContactNumber()
        {
            var t = (ContactNumberTextBox?.Text ?? "").Trim();
            if (string.IsNullOrEmpty(t))
            {
                _isContactNumberValid = false;
                ContactNumberErrorText.Text = "Required.";
                ContactNumberErrorText.Visibility = Visibility.Visible;
                return;
            }
            var digitsOnly = Regex.Replace(t, @"\D", "");
            var hasLetter = Regex.IsMatch(t, @"[A-Za-z]");
            if (digitsOnly.Length == 0 || hasLetter)
            {
                _isContactNumberValid = false;
                ContactNumberErrorText.Text = "Contact number must contain only numbers.";
                ContactNumberErrorText.Visibility = Visibility.Visible;
                return;
            }
            _isContactNumberValid = true;
            ContactNumberErrorText.Visibility = Visibility.Collapsed;
        }
        private void ValidateOrderNumber() { var t = OrderNumberTextBox?.Text?.Trim() ?? ""; _isOrderNumberValid = t.Length >= 1; }
        private void ValidateActivationDate() { _isActivationDateValid = ActivationDatePicker.SelectedDate.HasValue; ActivationDateErrorText.Visibility = _isActivationDateValid ? Visibility.Collapsed : Visibility.Visible; ActivationDateErrorText.Text = _isActivationDateValid ? "" : "Select a date."; }

        private void UpdateCreateButtonState()
        {
            CreateButton.IsEnabled = _isFirstNameValid && _isLastNameValid && _isEmailValid && _isPasswordValid && _isConfirmPasswordValid
                && _isMaxDevicesValid && _isPolicyNumberValid && _isOrganizationNameValid && _isContactPersonNameValid
                && _isCountryCodeValid && _isContactNumberValid && _isOrderNumberValid && _isActivationDateValid;
        }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            ErrorMessageText.Visibility = Visibility.Collapsed;
            ErrorMessageText.Text = "";

            var firstName = FirstNameTextBox.Text.Trim();
            var lastName = LastNameTextBox.Text.Trim();
            var email = EmailTextBox.Text.Trim();
            var password = PasswordRevealTextBox.Visibility == Visibility.Visible ? PasswordRevealTextBox.Text : PasswordBox.Password;
            var confirmPassword = ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible ? ConfirmPasswordRevealTextBox.Text : ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(firstName)) { ShowError("Please enter first name."); return; }
            if (string.IsNullOrWhiteSpace(lastName)) { ShowError("Please enter last name."); return; }
            if (string.IsNullOrWhiteSpace(email)) { ShowError("Please enter email."); return; }
            if (!IsValidEmail(email)) { ShowError("Please enter a valid email address."); return; }
            if (string.IsNullOrWhiteSpace(password)) { ShowError("Please enter a password."); return; }
            if (password.Length < 8) { ShowError("Password must be at least 8 characters long."); return; }
            if (!IsValidPassword(password)) { ShowError("Password must contain uppercase, lowercase, number, and special character."); return; }
            if (password != confirmPassword) { ShowError("Passwords do not match."); return; }
            if (!int.TryParse(MaxDevicesTextBox.Text.Trim(), out var maxDevices) || maxDevices <= 0) { ShowError("Please enter a valid Max Devices."); return; }

            var policyNumber = PolicyNumberTextBox.Text.Trim();
            var organizationName = OrganizationNameTextBox.Text.Trim();
            var contactPersonName = ContactPersonNameTextBox.Text.Trim();
            var countryCode = CountryCodeTextBox.Text.Trim();
            var contactNumber = ContactNumberTextBox.Text.Trim();
            var orderNumber = OrderNumberTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(policyNumber)) { ShowError("Policy Number is required."); return; }
            if (string.IsNullOrWhiteSpace(organizationName)) { ShowError("Organization Name is required."); return; }
            if (string.IsNullOrWhiteSpace(contactPersonName)) { ShowError("Contact Person Name is required."); return; }
            if (string.IsNullOrWhiteSpace(countryCode)) { ShowError("Country code is required."); return; }
            if (string.IsNullOrWhiteSpace(contactNumber)) { ShowError("Contact number is required."); return; }
            var contactDigits = Regex.Replace(contactNumber, @"\D", "");
            if (contactDigits.Length == 0 || Regex.IsMatch(contactNumber, @"[A-Za-z]"))
                { ShowError("Contact number must contain only numbers."); return; }
            if (string.IsNullOrWhiteSpace(orderNumber)) { ShowError("Order Number is required."); return; }
            if (!ActivationDatePicker.SelectedDate.HasValue) { ShowError("Activation Date is required."); return; }

            var activationDate = ActivationDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd") + "T00:00:00Z";

            CreateButton.Content = "Please wait...";
            CreateButton.IsEnabled = false;

            try
            {
                await _authService.RegisterCorpUserAsync(email, password, firstName, lastName, maxDevices, policyNumber, organizationName, contactPersonName, countryCode, contactNumber, orderNumber, activationDate);
                MessageBox.Show("Corp user created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                CorpAccountCreated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                CreateButton.Content = "Create Account";
                UpdateCreateButtonState();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

        private void ShowError(string message) { ErrorMessageText.Text = message; ErrorMessageText.Visibility = Visibility.Visible; }

        private static bool IsValidEmail(string email) { if (string.IsNullOrWhiteSpace(email)) return false; try { return new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase).IsMatch(email); } catch { return false; } }

        private static bool IsValidPassword(string password) { return new Regex(@"[A-Z]").IsMatch(password) && new Regex(@"[a-z]").IsMatch(password) && new Regex(@"\d").IsMatch(password) && new Regex(@"[@$!%*?&]").IsMatch(password); }
    }
}
