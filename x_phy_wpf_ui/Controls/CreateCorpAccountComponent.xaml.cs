using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class CreateCorpAccountComponent : UserControl
    {
        public event EventHandler BackRequested;
        public event EventHandler CorpAccountCreated;

        private readonly AuthService _authService;
        private int _currentStep = 1; // 1=Personal, 2=Organization, 3=Policy
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
        private DateTime? _selectedDate;
        private DateTime _displayMonth;
        // Prevents re-opening the popup when the input field click is what closed it
        private bool _suppressNextFieldClick;

        public CreateCorpAccountComponent()
        {
            InitializeComponent();
            _authService = new AuthService();
            Loaded += (s, e) =>
            {
                UpdatePlaceholders();
                UpdateStepVisibility();
                UpdateStepIndicator();
                UpdateNextButtonState();
            };
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
            _selectedDate = null;
            if (ActivationDateDisplay != null)
            {
                ActivationDateDisplay.Text = "Select a date";
                ActivationDateDisplay.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextSecondary");
            }
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
            _currentStep = 1;
            UpdatePlaceholders();
            UpdateStepVisibility();
            UpdateStepIndicator();
            UpdateNextButtonState();
        }

        private void UpdatePlaceholders()
        {
            if (FirstNamePlaceholder != null) FirstNamePlaceholder.Visibility = string.IsNullOrEmpty(FirstNameTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (LastNamePlaceholder != null) LastNamePlaceholder.Visibility = string.IsNullOrEmpty(LastNameTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (EmailPlaceholder != null) EmailPlaceholder.Visibility = string.IsNullOrEmpty(EmailTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (PasswordPlaceholder != null) PasswordPlaceholder.Visibility = (PasswordRevealTextBox?.Visibility == Visibility.Visible ? !string.IsNullOrEmpty(PasswordRevealTextBox.Text) : !string.IsNullOrEmpty(PasswordBox?.Password)) ? Visibility.Collapsed : Visibility.Visible;
            if (ConfirmPasswordPlaceholder != null) ConfirmPasswordPlaceholder.Visibility = (ConfirmPasswordRevealTextBox?.Visibility == Visibility.Visible ? !string.IsNullOrEmpty(ConfirmPasswordRevealTextBox.Text) : !string.IsNullOrEmpty(ConfirmPasswordBox?.Password)) ? Visibility.Collapsed : Visibility.Visible;
            if (OrganizationNamePlaceholder != null) OrganizationNamePlaceholder.Visibility = string.IsNullOrEmpty(OrganizationNameTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (ContactPersonNamePlaceholder != null) ContactPersonNamePlaceholder.Visibility = string.IsNullOrEmpty(ContactPersonNameTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (CountryCodePlaceholder != null) CountryCodePlaceholder.Visibility = string.IsNullOrEmpty(CountryCodeTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (ContactNumberPlaceholder != null) ContactNumberPlaceholder.Visibility = string.IsNullOrEmpty(ContactNumberTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (OrderNumberPlaceholder != null) OrderNumberPlaceholder.Visibility = string.IsNullOrEmpty(OrderNumberTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (PolicyNumberPlaceholder != null) PolicyNumberPlaceholder.Visibility = string.IsNullOrEmpty(PolicyNumberTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (MaxDevicesPlaceholder != null) MaxDevicesPlaceholder.Visibility = string.IsNullOrEmpty(MaxDevicesTextBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FirstNameTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateFirstName(); UpdateCreateButtonState(); }
        private void LastNameTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateLastName(); UpdateCreateButtonState(); }
        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateEmail(); UpdateCreateButtonState(); }
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) { if (PasswordRevealTextBox?.Visibility != Visibility.Visible) UpdatePlaceholders(); ValidatePassword(); ValidateConfirmPassword(); UpdateCreateButtonState(); }
        private void PasswordRevealTextBox_TextChanged(object sender, TextChangedEventArgs e) { if (PasswordRevealTextBox.Visibility == Visibility.Visible) { if (PasswordBox.Password != PasswordRevealTextBox.Text) PasswordBox.Password = PasswordRevealTextBox.Text; UpdatePlaceholders(); } ValidatePassword(); ValidateConfirmPassword(); UpdateCreateButtonState(); }
        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e) { if (ConfirmPasswordRevealTextBox?.Visibility != Visibility.Visible) UpdatePlaceholders(); ValidateConfirmPassword(); UpdateCreateButtonState(); }
        private void ConfirmPasswordRevealTextBox_TextChanged(object sender, TextChangedEventArgs e) { if (ConfirmPasswordRevealTextBox.Visibility == Visibility.Visible) { if (ConfirmPasswordBox.Password != ConfirmPasswordRevealTextBox.Text) ConfirmPasswordBox.Password = ConfirmPasswordRevealTextBox.Text; UpdatePlaceholders(); } ValidateConfirmPassword(); UpdateCreateButtonState(); }
        private void MaxDevicesTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateMaxDevices(); UpdateCreateButtonState(); }
        private void PolicyNumberTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidatePolicyNumber(); UpdateCreateButtonState(); }
        private void OrganizationNameTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateOrganizationName(); UpdateCreateButtonState(); }
        private void ContactPersonNameTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateContactPersonName(); UpdateCreateButtonState(); }
        private void CountryCodeTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateCountryCode(); UpdateCreateButtonState(); }
        private void ContactNumberTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateContactNumber(); UpdateCreateButtonState(); }
        private void OrderNumberTextBox_TextChanged(object sender, TextChangedEventArgs e) { UpdatePlaceholders(); ValidateOrderNumber(); UpdateCreateButtonState(); }

        private void FirstNameTextBox_LostFocus(object sender, RoutedEventArgs e) { ValidateFirstName(); UpdateCreateButtonState(); }
        private void LastNameTextBox_LostFocus(object sender, RoutedEventArgs e) { ValidateLastName(); UpdateCreateButtonState(); }
        private void EmailTextBox_LostFocus(object sender, RoutedEventArgs e) { ValidateEmail(); UpdateCreateButtonState(); }
        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e) { ValidatePassword(); ValidateConfirmPassword(); UpdateCreateButtonState(); }
        private void ConfirmPasswordBox_LostFocus(object sender, RoutedEventArgs e) { ValidateConfirmPassword(); UpdateCreateButtonState(); }
        private void OrganizationNameTextBox_LostFocus(object sender, RoutedEventArgs e) { ValidateOrganizationName(); UpdateCreateButtonState(); }
        private void ContactPersonNameTextBox_LostFocus(object sender, RoutedEventArgs e) { ValidateContactPersonName(); UpdateCreateButtonState(); }
        private void CountryCodeTextBox_LostFocus(object sender, RoutedEventArgs e) { ValidateCountryCode(); UpdateCreateButtonState(); }
        private void ContactNumberTextBox_LostFocus(object sender, RoutedEventArgs e) { ValidateContactNumber(); UpdateCreateButtonState(); }
        private void OrderNumberTextBox_LostFocus(object sender, RoutedEventArgs e) { ValidateOrderNumber(); UpdateCreateButtonState(); }
        private void PolicyNumberTextBox_LostFocus(object sender, RoutedEventArgs e) { ValidatePolicyNumber(); UpdateCreateButtonState(); }
        private void MaxDevicesTextBox_LostFocus(object sender, RoutedEventArgs e) { ValidateMaxDevices(); UpdateCreateButtonState(); }
        // ── Custom date picker ──────────────────────────────────────────────
        private void ActivationDateField_Click(object sender, MouseButtonEventArgs e)
        {
            // Suppress re-open when this click is the same one that just closed the popup
            if (_suppressNextFieldClick) return;

            if (CalendarPopup.IsOpen) { CalendarPopup.IsOpen = false; return; }

            if (_displayMonth == default)
                _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            BuildCalendar();
            CalendarPopup.IsOpen = true;
        }

        private void CalendarPopup_Opened(object sender, EventArgs e)
        {
            // Capture mouse to the popup border with SubTree mode so:
            //   • clicks inside the popup still route normally to buttons
            //   • clicks outside fire PreviewMouseDownOutsideCapturedElement → we close cleanly
            Mouse.Capture(CalendarPopupBorder, CaptureMode.SubTree);

            // Also close when the host window loses activation (user switches app/window)
            var window = Window.GetWindow(this);
            if (window != null)
                window.Deactivated += Window_Deactivated_CloseCalendar;
        }

        private void CalendarPopup_Closed(object sender, EventArgs e)
        {
            Mouse.Capture(null);

            var window = Window.GetWindow(this);
            if (window != null)
                window.Deactivated -= Window_Deactivated_CloseCalendar;
        }

        private void Window_Deactivated_CloseCalendar(object sender, EventArgs e)
        {
            CalendarPopup.IsOpen = false;
        }

        private void CalendarPopup_OutsideClick(object sender, MouseButtonEventArgs e)
        {
            // Set flag so the field's MouseDown handler (which fires right after this)
            // does not immediately re-open the popup when the click was on the input field.
            _suppressNextFieldClick = true;
            CalendarPopup.IsOpen = false;
            // Clear the flag after the current input-event chain finishes
            Dispatcher.BeginInvoke(new Action(() => _suppressNextFieldClick = false),
                DispatcherPriority.Input);
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _displayMonth = _displayMonth.AddMonths(-1);
            BuildCalendar();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            _displayMonth = _displayMonth.AddMonths(1);
            BuildCalendar();
        }

        private void BuildCalendar()
        {
            MonthYearLabel.Text = _displayMonth.ToString("MMMM yyyy");

            // Day-of-week header row
            DayOfWeekGrid.ColumnDefinitions.Clear();
            DayOfWeekGrid.Children.Clear();
            string[] dowLabels = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
            for (int i = 0; i < 7; i++)
            {
                DayOfWeekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var tb = new TextBlock
                {
                    Text = dowLabels[i],
                    TextAlignment = TextAlignment.Center,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2),
                };
                tb.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextSecondary");
                Grid.SetColumn(tb, i);
                DayOfWeekGrid.Children.Add(tb);
            }

            // Day buttons grid
            CalendarDaysGrid.ColumnDefinitions.Clear();
            CalendarDaysGrid.RowDefinitions.Clear();
            CalendarDaysGrid.Children.Clear();
            for (int i = 0; i < 7; i++)
                CalendarDaysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int startCol = (int)_displayMonth.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
            int rows = (int)Math.Ceiling((startCol + daysInMonth) / 7.0);
            for (int r = 0; r < rows; r++)
                CalendarDaysGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });

            for (int day = 1; day <= daysInMonth; day++)
            {
                int cell = startCol + day - 1;
                int row = cell / 7;
                int col = cell % 7;
                var dt = new DateTime(_displayMonth.Year, _displayMonth.Month, day);
                bool isSelected = _selectedDate.HasValue && _selectedDate.Value.Date == dt.Date;
                bool isToday = dt.Date == DateTime.Today;

                var btn = new Button
                {
                    Content = day.ToString(),
                    Width = 32, Height = 32,
                    FontSize = 12,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Tag = dt,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Template = _dayButtonTemplate ??= BuildDayButtonTemplate(),
                };

                if (isSelected)
                {
                    btn.SetResourceReference(Button.BackgroundProperty, "Brush.Secondary");
                    btn.Foreground = Brushes.White;
                }
                else if (isToday)
                {
                    btn.SetResourceReference(Button.BackgroundProperty, "Brush.Primary");
                    btn.Foreground = Brushes.White;
                }
                else
                {
                    btn.Background = Brushes.Transparent;
                    btn.SetResourceReference(Button.ForegroundProperty, "Brush.TextPrimary");
                }

                btn.Click += DayButton_Click;
                Grid.SetRow(btn, row);
                Grid.SetColumn(btn, col);
                CalendarDaysGrid.Children.Add(btn);
            }
        }

        private ControlTemplate _dayButtonTemplate;

        private static ControlTemplate BuildDayButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(16));
            border.SetBinding(Border.BackgroundProperty, new Binding("Background")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;
            return template;
        }

        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DateTime dt)
            {
                _selectedDate = dt;
                ActivationDateDisplay.Text = dt.ToString("MM/dd/yyyy");
                ActivationDateDisplay.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextPrimary");
                CalendarPopup.IsOpen = false;
                ValidateActivationDate();
                UpdateCreateButtonState();
            }
        }

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

        private void PasswordInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordRequirementsPopup != null)
                PasswordRequirementsPopup.IsOpen = !PasswordRequirementsPopup.IsOpen;
        }

        private void ValidateFirstName() { var t = FirstNameTextBox?.Text?.Trim() ?? ""; _isFirstNameValid = t.Length >= 1; FirstNameErrorText.Visibility = _isFirstNameValid ? Visibility.Collapsed : Visibility.Visible; FirstNameErrorText.Text = _isFirstNameValid ? "" : "Required."; }
        private void ValidateLastName() { var t = LastNameTextBox?.Text?.Trim() ?? ""; _isLastNameValid = t.Length >= 1; LastNameErrorText.Visibility = _isLastNameValid ? Visibility.Collapsed : Visibility.Visible; LastNameErrorText.Text = _isLastNameValid ? "" : "Required."; }
        private void ValidateEmail() { var t = EmailTextBox?.Text?.Trim() ?? ""; _isEmailValid = IsValidEmail(t); EmailErrorText.Visibility = _isEmailValid ? Visibility.Collapsed : Visibility.Visible; EmailErrorText.Text = _isEmailValid ? "" : "Valid email required."; }
        private void ValidatePassword() { var p = PasswordRevealTextBox?.Visibility == Visibility.Visible ? PasswordRevealTextBox?.Text : PasswordBox?.Password; _isPasswordValid = !string.IsNullOrEmpty(p) && p.Length >= 8 && !p.Contains(" ") && IsValidPassword(p); PasswordErrorText.Visibility = _isPasswordValid ? Visibility.Collapsed : Visibility.Visible; PasswordErrorText.Text = _isPasswordValid ? "" : "Min 8 chars, upper, lower, number, one special (no spaces)."; }
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
        private void ValidateActivationDate() { _isActivationDateValid = _selectedDate.HasValue; ActivationDateErrorText.Visibility = _isActivationDateValid ? Visibility.Collapsed : Visibility.Visible; ActivationDateErrorText.Text = _isActivationDateValid ? "" : "Select a date."; }

        private void UpdateStepVisibility()
        {
            if (Step1Panel == null || Step2Panel == null || Step3Panel == null) return;
            // Step 1 = Personal, Step 2 = Organization, Step 3 = Policy — only one panel visible at a time
            Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            // Personal: Back hidden, Next visible. Organization: Back + Next visible. Policy: Back + Create visible, Next hidden.
            if (BackButton != null)
                BackButton.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
            if (NextButton != null)
                NextButton.Visibility = _currentStep <= 2 ? Visibility.Visible : Visibility.Collapsed;
            if (CreateButton != null)
                CreateButton.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateStepIndicator()
        {
            var cyan = (System.Windows.Media.Brush)Application.Current.FindResource("Brush.Secondary");
            var grey = (System.Windows.Media.Brush)Application.Current.FindResource("Brush.TextSecondary");
            bool active1 = _currentStep == 1, active2 = _currentStep == 2, active3 = _currentStep == 3;
            if (Step1Circle != null) Step1Circle.BorderBrush = active1 ? cyan : grey;
            if (Step1Number != null) Step1Number.Foreground = active1 ? cyan : grey;
            if (Step1Label != null) Step1Label.Foreground = active1 ? cyan : grey;
            if (Step2Circle != null) Step2Circle.BorderBrush = active2 ? cyan : grey;
            if (Step2Number != null) Step2Number.Foreground = active2 ? cyan : grey;
            if (Step2Label != null) Step2Label.Foreground = active2 ? cyan : grey;
            if (Step3Circle != null) Step3Circle.BorderBrush = active3 ? cyan : grey;
            if (Step3Number != null) Step3Number.Foreground = active3 ? cyan : grey;
            if (Step3Label != null) Step3Label.Foreground = active3 ? cyan : grey;
        }

        private void UpdateNextButtonState()
        {
            if (_currentStep == 1)
            {
                bool step1Valid = _isFirstNameValid && _isLastNameValid && _isEmailValid && _isPasswordValid && _isConfirmPasswordValid;
                if (NextButton != null)
                    NextButton.IsEnabled = step1Valid;
            }
            else if (_currentStep == 2)
            {
                bool step2Valid = _isOrganizationNameValid && _isContactPersonNameValid && _isCountryCodeValid && _isContactNumberValid && _isOrderNumberValid;
                if (NextButton != null)
                    NextButton.IsEnabled = step2Valid;
            }
            else
            {
                if (CreateButton != null)
                    CreateButton.IsEnabled = _isMaxDevicesValid && _isPolicyNumberValid && _isActivationDateValid;
            }
        }

        private void UpdateCreateButtonState()
        {
            UpdateNextButtonState();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 1)
            {
                ValidateFirstName();
                ValidateLastName();
                ValidateEmail();
                ValidatePassword();
                ValidateConfirmPassword();
                UpdateNextButtonState();
                _currentStep = 2;
            }
            else if (_currentStep == 2)
            {
                ValidateOrganizationName();
                ValidateContactPersonName();
                ValidateCountryCode();
                ValidateContactNumber();
                ValidateOrderNumber();
                UpdateNextButtonState();
                _currentStep = 3;
            }
            UpdateStepVisibility();
            UpdateStepIndicator();
            UpdateNextButtonState();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepVisibility();
                UpdateStepIndicator();
                UpdateNextButtonState();
            }
            else
                BackRequested?.Invoke(this, EventArgs.Empty);
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
            if (!_selectedDate.HasValue) { ShowError("Activation Date is required."); return; }

            var activationDate = _selectedDate.Value.ToString("yyyy-MM-dd") + "T00:00:00Z";

            CreateButton.Content = "Please wait...";
            CreateButton.IsEnabled = false;

            try
            {
                await _authService.RegisterCorpUserAsync(email, password, firstName, lastName, maxDevices, policyNumber, organizationName, contactPersonName, countryCode, contactNumber, orderNumber, activationDate);
                AppDialog.Show(Window.GetWindow(this), "Corp user created successfully.", "Success", MessageBoxImage.Information);
                CorpAccountCreated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ShowError(ToUserFriendlyError(ex.Message));
            }
            finally
            {
                CreateButton.Content = "Create Account";
                UpdateCreateButtonState();
            }
        }

        private void ShowError(string message) { ErrorMessageText.Text = message; ErrorMessageText.Visibility = Visibility.Visible; }

        private static string ToUserFriendlyError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "Something went wrong. Please try again or contact support.";
            var m = message.Trim();
            if (m.IndexOf("MachineLimitExceeded", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("machine limit exceeded", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("policy machine limit", StringComparison.OrdinalIgnoreCase) >= 0)
                return "This policy has reached its machine limit. You cannot add more machines. Please contact your administrator or use a different policy.";
            if (m.IndexOf("BAD_REQUEST_BODY", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (m.IndexOf("regex", StringComparison.OrdinalIgnoreCase) >= 0 && m.IndexOf("match", StringComparison.OrdinalIgnoreCase) >= 0) ||
                m.IndexOf("format attribute", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("int32", StringComparison.OrdinalIgnoreCase) >= 0)
                return "The policy number format is invalid. Please enter a valid policy number (check with your administrator for the correct format).";
            if (m.IndexOf("User created but license purchase failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                m.IndexOf("license purchase failed", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Your account was created, but we couldn't complete the license setup. Please contact support or try again later.";
            if (m.IndexOf("Network error", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Please check your internet connection and try again.";
            if (m.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Request timed out. Please check your connection and try again.";
            if (m.IndexOf("Registration error:", StringComparison.OrdinalIgnoreCase) >= 0)
                return "We couldn't complete registration. Please check your details and try again, or contact support.";
            return "Something went wrong. Please try again or contact support.";
        }

        private static bool IsValidEmail(string email) { if (string.IsNullOrWhiteSpace(email)) return false; try { return new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase).IsMatch(email); } catch { return false; } }

        private static bool IsValidPassword(string password) { return !string.IsNullOrEmpty(password) && !password.Contains(" ") && new Regex(@"[A-Z]").IsMatch(password) && new Regex(@"[a-z]").IsMatch(password) && new Regex(@"\d").IsMatch(password) && new Regex(@"[^a-zA-Z0-9\s]").IsMatch(password); }
    }
}
