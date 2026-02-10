using System;
using System.Windows;
using x_phy_wpf_ui.Controls;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui
{
    public partial class LaunchWindow : Window
    {
        private WelcomeComponent _welcomeComponent;
        private LaunchComponent _launchComponent;
        private SignInComponent _signInComponent;
        private CreateAccountComponent _createAccountComponent;

        public LaunchWindow()
        {
            InitializeComponent();
            
            // Start with WelcomeComponent
            ShowWelcomeComponent();
        }

        public LaunchWindow(bool showSignIn) : this()
        {
            if (showSignIn)
            {
                // Skip welcome and show sign in directly
                ShowSignInComponent();
            }
        }

        private void ShowWelcomeComponent()
        {
            if (_welcomeComponent == null)
            {
                _welcomeComponent = new WelcomeComponent();
                _welcomeComponent.NavigateToLaunch += WelcomeComponent_NavigateToLaunch;
            }
            ComponentContainer.Content = _welcomeComponent;
        }

        private void WelcomeComponent_NavigateToLaunch(object sender, EventArgs e)
        {
            ShowLaunchComponent();
        }

        private void ShowLaunchComponent()
        {
            if (_launchComponent == null)
            {
                _launchComponent = new LaunchComponent();
                _launchComponent.NavigateToSignIn += LaunchComponent_NavigateToSignIn;
                _launchComponent.NavigateToCreateAccount += LaunchComponent_NavigateToCreateAccount;
            }
            ComponentContainer.Content = _launchComponent;
        }

        private void LaunchComponent_NavigateToSignIn(object sender, EventArgs e)
        {
            ShowSignInComponent();
        }

        private void LaunchComponent_NavigateToCreateAccount(object sender, EventArgs e)
        {
            ShowCreateAccountComponent();
        }

        private void ShowSignInComponent()
        {
            if (_signInComponent == null)
            {
                _signInComponent = new SignInComponent();
                _signInComponent.NavigateToCreateAccount += SignInComponent_NavigateToCreateAccount;
                _signInComponent.SignInSuccessful += SignInComponent_SignInSuccessful;
                _signInComponent.NavigateBack += (s, e) => ShowLaunchComponent();
            }
            ComponentContainer.Content = _signInComponent;
        }

        private void SignInComponent_NavigateToCreateAccount(object sender, EventArgs e)
        {
            ShowCreateAccountComponent();
        }

        private void SignInComponent_SignInSuccessful(object sender, EventArgs e)
        {
            // Launch screen: do NOT validate license (no Keygen check). Save tokens and open MainWindow.
            // License is validated only on Sign In inside MainWindow; machine error is shown there.
            if (e is not SignInSuccessfulEventArgs args || args.LoginResponse == null)
            {
                ComponentContainer.Content = _signInComponent;
                return;
            }

            if (!string.IsNullOrWhiteSpace(args.LicenseKey))
            {
                try { MainWindow.WriteLicenseKeyToExeConfig(args.LicenseKey.Trim()); }
                catch { /* best effort */ }
            }

            var response = args.LoginResponse;
            var licenseInfo = response.License ?? (response.User != null
                ? new LicenseInfo
                {
                    Status = string.IsNullOrEmpty(response.User.LicenseStatus) ? "Trial" : response.User.LicenseStatus,
                    TrialEndsAt = response.User.TrialEndsAt
                }
                : null);
            var tokenStorage = new TokenStorage();
            tokenStorage.SaveTokens(
                response.AccessToken,
                response.RefreshToken,
                response.ExpiresIn,
                response.User.Id,
                response.User.Username,
                response.User,
                licenseInfo
            );

            var allWindows = new System.Collections.Generic.List<Window>();
            foreach (Window window in Application.Current.Windows)
                allWindows.Add(window);
            foreach (var window in allWindows)
                window.Close();

            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open main window: {ex.Message}\n\nPlease try again or contact support.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowCreateAccountComponent()
        {
            if (_createAccountComponent == null)
            {
                _createAccountComponent = new CreateAccountComponent();
                _createAccountComponent.NavigateToSignIn += CreateAccountComponent_NavigateToSignIn;
                _createAccountComponent.AccountCreated += CreateAccountComponent_AccountCreated;
                _createAccountComponent.NavigateBack += (s, e) => ShowLaunchComponent();
            }
            ComponentContainer.Content = _createAccountComponent;
        }

        private void CreateAccountComponent_NavigateToSignIn(object sender, EventArgs e)
        {
            ShowSignInComponent();
        }

        private void CreateAccountComponent_AccountCreated(object sender, EventArgs e)
        {
            // After account creation, navigate to sign in
            ShowSignInComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
