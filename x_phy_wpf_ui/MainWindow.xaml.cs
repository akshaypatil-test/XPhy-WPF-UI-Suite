using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using XPhyWrapper;
using x_phy_wpf_ui.Services;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Controls;
using static x_phy_wpf_ui.Services.ThemeManager;

namespace x_phy_wpf_ui
{
    public partial class MainWindow : Window
    {
        private ApplicationControllerWrapper controller;
        private DispatcherTimer statusTimer;
        private bool isMaximized = false;
        private bool? overallClassification = null; // null = unknown, true = Deepfake, false = Real
        /// <summary>True if deepfake was detected at any time during the current run; used for final "Detection Completed" notification.</summary>
        private bool _deepfakeDetectedDuringRun = false;
        private bool isWebSurfingMode = false; // Track if current detection is Web Surfing mode
        private bool isStoppingDetection = false; // Track if we're in the process of stopping
        /// <summary>When true, show Results tab and Session Details after stop completes (e.g. after "Stop & View Results" from notification).</summary>
        private bool openResultsFolderAfterStop = false;
        /// <summary>When true, CreateResult was already called from "Stop & View Results" click; ShowFinalResult should skip PushDetectionResultToBackend to avoid duplicate.</summary>
        private bool _resultPushedForStopAndViewResults = false;
        /// <summary>True when we just navigated to Results after "Stop & View Results"; finally block should skip ResetAppContentToHome so user stays on Results.</summary>
        private bool _openedResultsAfterStop = false;
        private bool isAudioDetection = false; // Track if current detection is Audio mode (vs Video mode)
        /// <summary>Display name for current detection source (e.g. "Zoom", "Google Chrome") for CreateResult MediaSource.</summary>
        private string _currentMediaSourceDisplayName = "Local";
        /// <summary>When set by floating launcher "Stop & View Results", use this path for opening results so they match the saved result.</summary>
        private string _pathForResultsAfterStop;
        private LicenseManager licenseManager;
        private ResultsApiService _resultsApiService;
        private bool controllerInitializationAttempted = false; // Track if we've already tried to initialize
        private int _inferenceEnvRetryCount = 0; // Used for one-time retry when starting detection on first run
        private Task _inferenceWarmUpTask; // Warm-up runs after controller init; we wait for it before first Start detection

        // Auth view components (Shell: Welcome → Get Started → Login/Signup; after logout → Login directly)
        private WelcomeComponent _welcomeComponent;
        private LaunchComponent _launchComponent;
        private SignInComponent _signInComponent;
        private LoaderComponent _loaderComponent;
        private DateTime _loaderShownAt;
        private CreateAccountComponent _createAccountComponent;
        private EmailVerificationComponent _emailVerificationComponent;
        private AccountVerifiedComponent _accountVerifiedComponent;
        private RecoverUsernameComponent _recoverUsernameComponent;
        private ForgotUsernameSuccessComponent _forgotUsernameSuccessComponent;
        private ForgotPasswordComponent _forgotPasswordComponent;
        private ForgotPasswordVerifyOtpComponent _forgotPasswordVerifyOtpComponent;
        private ResetPasswordComponent _resetPasswordComponent;
        private CorporateSignInComponent _corporateSignInComponent;
        private UpdatePasswordComponent _updatePasswordComponent;
        private bool _appViewShownOnce = false;
        private ChangePasswordDialog _changePasswordDialog;
        private VerifyChangePasswordOtpDialog _verifyChangePasswordOtpDialog;
        private PasswordChangedSuccessDialog _passwordChangedSuccessDialog;
        private AuthService _authServiceForChangePassword;
        private string _changePasswordCurrent = "";
        private string _changePasswordNew = "";
        private FloatingWidgetWindow _floatingWidget;
        /// <summary>Show floating widget when detection is running and app is minimized (arc loader: green, red when deepfake).</summary>
        private const bool FloatingWidgetEnabled = true;

        /// <summary>True when user chose Exit from tray menu; allows actual close instead of minimize-to-tray.</summary>
        private bool _isExitingFromTray = false;

        /// <summary>When minimized to tray, run process detection periodically; show single-process popup when exactly one app is detected.</summary>
        private DispatcherTimer _backgroundProcessCheckTimer;
        private DateTime _lastMediaSourcePopupShownAt = DateTime.MinValue;
        private const int MediaSourcePopupCooldownSeconds = 30 * 60; // 30 minutes
        /// <summary>True when we've seen 0 listed processes since last popup; allows showing again when user closes app and reopens it.</summary>
        private bool _hasSeenZeroProcessesSinceLastPopup = true;
        /// <summary>Last time we showed the "Multiple Media Source Detected" popup; 30-min cooldown.</summary>
        private DateTime _lastMultipleSourcesPopupShownAt = DateTime.MinValue;

        // Helper method to get color resources
        private SolidColorBrush GetResourceBrush(string resourceKey)
        {
            return (SolidColorBrush)this.Resources[resourceKey];
        }

        public MainWindow() : this(null) { }

        /// <summary>
        /// Use when opening from LaunchWindow after sign-in: controller was already created and license validated there.
        /// </summary>
        public MainWindow(ApplicationControllerWrapper preInitializedController)
        {
            try
            {
                if (preInitializedController != null)
                {
                    controller = preInitializedController;
                    controllerInitializationAttempted = true;
                }

                InitializeComponent();

                // Initialize timers (these are safe to initialize immediately)
                InitializeStatusTimer();
                
                // Initialize license manager (safe to initialize immediately)
                try
                {
                    InitializeLicenseManager();
                }
                catch (Exception ex)
                {
                    // Log but don't block window creation if license manager fails
                    System.Diagnostics.Debug.WriteLine($"License manager initialization failed: {ex.Message}");
                }

                _resultsApiService = new ResultsApiService();

                // When user clicks "Back to Results" from Session Details, refresh the list so any record saved in the meantime (e.g. after completion) appears
                if (DetectionResultsScreen != null)
                    DetectionResultsScreen.BackToResultsListRequested += OnBackToResultsListRequested;
                
                // Enable window dragging
                this.MouseDown += MainWindow_MouseDown;
                
                // Ensure window fits on screen
                this.Loaded += MainWindow_Loaded;

                // Refresh license/status when user returns to MainWindow (e.g. after purchase in another window)
                this.Activated += MainWindow_Activated;
                
                // Handle window state changes
                this.StateChanged += MainWindow_StateChanged;

                // When refresh token expires or is invalid, redirect to sign-in
                AuthenticatedApiClient.SessionExpired += AuthenticatedApiClient_SessionExpired;

                // Logout on close only when Remember Me was unchecked at login
                this.Closing += MainWindow_Closing;

                // Start/stop background single-process detection when minimizing to tray / restoring
                this.IsVisibleChanged += MainWindow_IsVisibleChanged;
                
                // Shell: Set up auth view (Login/Signup). Controller init happens when we show AppView after login.
                SetupAuthView();
            }
            catch (Exception ex)
            {
                // Log the error and rethrow (components now handle sign-in)
                System.Diagnostics.Debug.WriteLine($"MainWindow constructor error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw so caller can handle it
            }
        }

        private void SetupAuthView()
        {
            _welcomeComponent = new WelcomeComponent();
            _launchComponent = new LaunchComponent();
            _signInComponent = new SignInComponent();
            _loaderComponent = new LoaderComponent();
            _createAccountComponent = new CreateAccountComponent();
            _emailVerificationComponent = new EmailVerificationComponent();
            _accountVerifiedComponent = new AccountVerifiedComponent();
            _recoverUsernameComponent = new RecoverUsernameComponent();
            _forgotUsernameSuccessComponent = new ForgotUsernameSuccessComponent();
            _forgotPasswordComponent = new ForgotPasswordComponent();
            _forgotPasswordVerifyOtpComponent = new ForgotPasswordVerifyOtpComponent();
            _resetPasswordComponent = new ResetPasswordComponent();
            _corporateSignInComponent = new CorporateSignInComponent();
            _updatePasswordComponent = new UpdatePasswordComponent();
            _changePasswordDialog = new ChangePasswordDialog();
            _verifyChangePasswordOtpDialog = new VerifyChangePasswordOtpDialog();
            _passwordChangedSuccessDialog = new PasswordChangedSuccessDialog();
            _authServiceForChangePassword = new AuthService();
            ChangePasswordOverlayContent.Content = _changePasswordDialog;
            _changePasswordDialog.BackRequested += (s, ev) => { ChangePasswordOverlay.Visibility = Visibility.Collapsed; };
            _changePasswordDialog.UpdatePasswordRequested += ChangePasswordDialog_UpdatePasswordRequested;
            _verifyChangePasswordOtpDialog.VerifyRequested += VerifyChangePasswordOtpDialog_VerifyRequested;
            _verifyChangePasswordOtpDialog.ResendRequested += VerifyChangePasswordOtpDialog_ResendRequested;
            _verifyChangePasswordOtpDialog.CloseRequested += (s, ev) => { ChangePasswordOverlay.Visibility = Visibility.Collapsed; };

            // Initial app start: Welcome → Get Started (Launch) → Sign In / Create Account / Corporate Sign In
            _welcomeComponent.NavigateToLaunch += (s, e) => { AuthPanel.SetContent(_launchComponent); };
            _launchComponent.NavigateToSignIn += (s, e) => { _signInComponent.ClearInputs(); AuthPanel.SetContent(_signInComponent); };
            _launchComponent.NavigateToCreateAccount += (s, e) => { _createAccountComponent.ClearInputs(); AuthPanel.SetContent(_createAccountComponent); };
            _launchComponent.NavigateToCorporateSignIn += (s, e) => { _corporateSignInComponent.ClearInputs(); AuthPanel.SetContent(_corporateSignInComponent); };
            
            _signInComponent.NavigateToCreateAccount += (s, e) => { _createAccountComponent.ClearInputs(); AuthPanel.SetContent(_createAccountComponent); };
            _signInComponent.NavigateToRecoverUsername += (s, e) => { _recoverUsernameComponent.ClearInputs(); AuthPanel.SetContent(_recoverUsernameComponent); };
            _signInComponent.NavigateToForgotPassword += (s, e) => { _forgotPasswordComponent.ClearInputs(); AuthPanel.SetContent(_forgotPasswordComponent); };
            _signInComponent.ShowLoaderRequested += (s, e) =>
            {
                _loaderShownAt = DateTime.UtcNow;
                AuthPanel.SetContent(_loaderComponent);
            };
            _signInComponent.SignInSuccessful += SignInComponent_SignInSuccessful;
            _signInComponent.SignInFailed += (s, e) =>
            {
                AuthPanel.SetContent(_signInComponent);
                if (e != null && !string.IsNullOrEmpty(e.Message))
                    _signInComponent.SetError(e.Message);
            };
            _signInComponent.NavigateBack += (s, e) => { AuthPanel.SetContent(_launchComponent); };
            
            _createAccountComponent.NavigateToSignIn += (s, e) => { _signInComponent.ClearInputs(); AuthPanel.SetContent(_signInComponent); };
            _createAccountComponent.NavigateBack += (s, e) => { AuthPanel.SetContent(_launchComponent); };
            _createAccountComponent.AccountCreated += (s, e) => { _signInComponent.ClearInputs(); AuthPanel.SetContent(_signInComponent); };
            _createAccountComponent.NavigateToEmailVerification += (s, e) =>
            {
                _emailVerificationComponent.ClearInputs();
                _emailVerificationComponent.SetEmail(e.Email);
                AuthPanel.SetContent(_emailVerificationComponent);
            };
            
            _emailVerificationComponent.NavigateBack += (s, e) => { _createAccountComponent.ClearInputs(); AuthPanel.SetContent(_createAccountComponent); };
            _emailVerificationComponent.NavigateToAccountVerified += (s, e) => { AuthPanel.SetContent(_accountVerifiedComponent); };
            
            _accountVerifiedComponent.NavigateToSignIn += (s, e) => { _signInComponent.ClearInputs(); AuthPanel.SetContent(_signInComponent); };
            
            _recoverUsernameComponent.NavigateBack += (s, e) => { _signInComponent.ClearInputs(); AuthPanel.SetContent(_signInComponent); };
            _recoverUsernameComponent.NavigateToSuccess += (s, e) => { AuthPanel.SetContent(_forgotUsernameSuccessComponent); };
            _forgotUsernameSuccessComponent.NavigateToSignIn += (s, e) => { _signInComponent.ClearInputs(); AuthPanel.SetContent(_signInComponent); };

            _forgotPasswordComponent.NavigateBack += (s, e) => { _signInComponent.ClearInputs(); AuthPanel.SetContent(_signInComponent); };
            _forgotPasswordComponent.NavigateToVerifyOtp += (s, email) =>
            {
                _forgotPasswordVerifyOtpComponent.ClearInputs();
                _forgotPasswordVerifyOtpComponent.SetEmail(email);
                AuthPanel.SetContent(_forgotPasswordVerifyOtpComponent);
            };
            _forgotPasswordVerifyOtpComponent.NavigateBack += (s, e) => { _forgotPasswordComponent.ClearInputs(); AuthPanel.SetContent(_forgotPasswordComponent); };
            _forgotPasswordVerifyOtpComponent.NavigateToResetPassword += (s, resetToken) =>
            {
                _resetPasswordComponent.ClearInputs();
                _resetPasswordComponent.SetResetToken(resetToken);
                AuthPanel.SetContent(_resetPasswordComponent);
            };
            _resetPasswordComponent.NavigateBack += (s, e) => { _signInComponent.ClearInputs(); AuthPanel.SetContent(_signInComponent); };
            _resetPasswordComponent.NavigateToSignIn += (s, e) => { _signInComponent.ClearInputs(); AuthPanel.SetContent(_signInComponent); };

            _corporateSignInComponent.NavigateBack += (s, e) => { AuthPanel.SetContent(_launchComponent); };
            _corporateSignInComponent.NavigateToRecoverUsername += (s, e) => { _recoverUsernameComponent.ClearInputs(); AuthPanel.SetContent(_recoverUsernameComponent); };
            _corporateSignInComponent.NavigateToForgotPassword += (s, e) => { _forgotPasswordComponent.ClearInputs(); AuthPanel.SetContent(_forgotPasswordComponent); };
            _corporateSignInComponent.ShowLoaderRequested += (s, e) =>
            {
                _loaderShownAt = DateTime.UtcNow;
                AuthPanel.SetContent(_loaderComponent);
            };
            _corporateSignInComponent.SignInSuccessful += SignInComponent_SignInSuccessful;
            _corporateSignInComponent.SignInRequiresPasswordChange += (s, required) =>
            {
                _updatePasswordComponent.ClearInputs();
                AuthPanel.SetContent(_updatePasswordComponent);
            };
            _corporateSignInComponent.SignInFailed += (s, e) =>
            {
                AuthPanel.SetContent(_corporateSignInComponent);
                if (e != null && !string.IsNullOrEmpty(e.Message))
                    _corporateSignInComponent.SetError(e.Message);
            };

            _updatePasswordComponent.PasswordUpdated += (s, e) => SignInComponent_SignInSuccessful(s, e);

            // First app start: show Welcome, then it auto-navigates to Get Started, then user picks Sign In / Create Account
            AuthPanel.SetContent(_welcomeComponent);
            AuthPanel.CloseRequested += AuthHostView_CloseRequested;
        }

        private async void SignInComponent_SignInSuccessful(object sender, EventArgs e)
        {
            if (e is not SignInSuccessfulEventArgs args || args.LoginResponse == null)
            {
                // PasswordUpdated from corporate first-time: validate using stored tokens and show app.
                Dispatcher.Invoke(() => TryValidateStoredTokensAndShowApp());
                return;
            }

            UserControl signInComponentOnError = args.FromCorporateSignIn ? _corporateSignInComponent : _signInComponent;

            // 1. Write/override config.toml with the user's license key (AppData, no admin needed).
            if (!string.IsNullOrWhiteSpace(args.LicenseKey))
                WriteLicenseKeyToExeConfig(args.LicenseKey.Trim());
            else
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("No license key received. Cannot validate license.", "License Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    AuthPanel.SetContent(signInComponentOnError);
                });
                return;
            }

            var response = args.LoginResponse;
            var licenseInfo = response.License ?? (response.User != null
                ? new LicenseInfo
                {
                    Status = string.IsNullOrEmpty(response.User.LicenseStatus) ? "Trial" : response.User.LicenseStatus,
                    TrialEndsAt = response.User.TrialEndsAt
                }
                : null);
            string licenseStatus = licenseInfo?.Status ?? response.User?.LicenseStatus ?? "Trial";
            bool isExpiredFromLms = licenseStatus.Trim().Equals("Expired", StringComparison.OrdinalIgnoreCase);

            var elapsed = (DateTime.UtcNow - _loaderShownAt).TotalSeconds;
            if (elapsed < 3.0 && !isExpiredFromLms)
            {
                var delayMs = (int)((3.0 - elapsed) * 1000);
                await Task.Delay(delayMs);
            }

            // 2. When LMS says Expired: allow login without controller; save tokens and show app (expired UI). Otherwise run Keygen validation.
            Dispatcher.Invoke(() =>
            {
                var tokenStorage = new TokenStorage();
                if (!isExpiredFromLms)
                {
                    try
                    {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string outputDir = Path.Combine(appData, "X-PHY", "X-PHY Deepfake Detector");
                        Directory.CreateDirectory(outputDir);
                        string configPath = GetAppDataConfigPath();
                        controller = new ApplicationControllerWrapper(outputDir, configPath);
                        controllerInitializationAttempted = true;
                        System.Diagnostics.Debug.WriteLine("License validation (Keygen) succeeded at sign-in.");
                        var ctrl = controller;
                        _inferenceWarmUpTask = Task.Run(() =>
                        {
                            try
                            {
                                System.Threading.Thread.Sleep(3000);
                                var method = ctrl?.GetType().GetMethod("PrepareInferenceEnvironment", Type.EmptyTypes);
                                if (method != null) method.Invoke(ctrl, null);
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Warm-up: {ex.Message}"); }
                        });
                    }
                    catch (Exception ex)
                    {
                        controller = null;
                        controllerInitializationAttempted = true;
                        if (IsLicenseExpiredFailure(ex))
                        {
                            System.Diagnostics.Debug.WriteLine("Login: Keygen reported license expired; allowing login and showing expired UI.");
                            var expiredLicenseInfo = licenseInfo != null
                                ? new LicenseInfo
                                {
                                    Key = licenseInfo.Key,
                                    Status = "Expired",
                                    MaxDevices = licenseInfo.MaxDevices,
                                    TrialAttemptsRemaining = 0,
                                    TrialEndsAt = licenseInfo.TrialEndsAt,
                                    PurchaseDate = licenseInfo.PurchaseDate,
                                    ExpiryDate = licenseInfo.ExpiryDate,
                                    PlanId = licenseInfo.PlanId,
                                    PlanName = licenseInfo.PlanName
                                }
                                : new LicenseInfo { Key = args.LicenseKey?.Trim(), Status = "Expired" };
                            var userExpired = response.User != null ? new UserInfo { Id = response.User.Id, Username = response.User.Username, LicenseStatus = "Expired", TrialEndsAt = response.User.TrialEndsAt, UserType = response.User.UserType } : response.User;
                            tokenStorage.SaveTokens(
                                response.AccessToken,
                                response.RefreshToken,
                                response.ExpiresIn,
                                response.User.Id,
                                response.User.Username,
                                userExpired,
                                expiredLicenseInfo,
                                args.RememberMe
                            );
                            ShowAppView();
                            return;
                        }
                        string message = GetControllerInitFailureMessage(ex);
                        bool isLicenseError = IsLicenseValidationFailure(ex);
                        string title = isLicenseError ? "License Error" : "Validation Failed";
                        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                        AuthPanel.SetContent(signInComponentOnError);
                        return;
                    }
                }
                else
                {
                    controllerInitializationAttempted = true;
                    System.Diagnostics.Debug.WriteLine("Login allowed with Expired license from LMS; app will show expired UI.");
                }

                // 3. Normalize license (Expired + 0 attempts if actually expired) so we never persist stale Trial from DB; save with user LicenseStatus in sync.
                var licenseToSave = NormalizeLicenseFromApi(licenseInfo) ?? licenseInfo;
                var userToSave = response.User != null
                    ? new UserInfo { Id = response.User.Id, Username = response.User.Username, LicenseStatus = licenseToSave.Status, TrialEndsAt = response.User.TrialEndsAt, UserType = response.User.UserType }
                    : response.User;
                tokenStorage.SaveTokens(
                    response.AccessToken,
                    response.RefreshToken,
                    response.ExpiresIn,
                    response.User.Id,
                    response.User.Username,
                    userToSave,
                    licenseToSave,
                    args.RememberMe
                );
                ShowAppView();
            });
        }

        /// <summary>Used after corporate first-time password update: validate license from stored tokens and show app.</summary>
        private void TryValidateStoredTokensAndShowApp()
        {
            var tokenStorage = new TokenStorage();
            var storedTokens = tokenStorage.GetTokens();
            string? licenseKey = storedTokens?.LicenseInfo?.Key;
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                AuthPanel.SetContent(_signInComponent);
                return;
            }
            string licenseStatus = storedTokens?.LicenseInfo?.Status ?? storedTokens?.UserInfo?.LicenseStatus ?? "";
            bool isExpiredFromLms = licenseStatus.Trim().Equals("Expired", StringComparison.OrdinalIgnoreCase);

            WriteLicenseKeyToExeConfig(licenseKey.Trim());
            if (isExpiredFromLms)
            {
                controllerInitializationAttempted = true;
                ShowAppView();
                return;
            }
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string outputDir = Path.Combine(appData, "X-PHY", "X-PHY Deepfake Detector");
                Directory.CreateDirectory(outputDir);
                string configPath = GetAppDataConfigPath();
                controller = new ApplicationControllerWrapper(outputDir, configPath);
                controllerInitializationAttempted = true;
                var ctrl = controller;
                _inferenceWarmUpTask = Task.Run(() =>
                {
                    try
                    {
                        System.Threading.Thread.Sleep(3000);
                        var method = ctrl?.GetType().GetMethod("PrepareInferenceEnvironment", Type.EmptyTypes);
                        if (method != null) method.Invoke(ctrl, null);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Warm-up: {ex.Message}"); }
                });
                ShowAppView();
            }
            catch (Exception ex)
            {
                controller = null;
                controllerInitializationAttempted = true;
                if (IsLicenseExpiredFailure(ex))
                {
                    System.Diagnostics.Debug.WriteLine("TryValidateStoredTokens: Keygen reported license expired; showing app with expired UI.");
                    var stored = tokenStorage.GetTokens();
                    if (stored?.LicenseInfo != null)
                    {
                        var updated = new LicenseInfo
                        {
                            Key = stored.LicenseInfo.Key,
                            Status = "Expired",
                            MaxDevices = stored.LicenseInfo.MaxDevices,
                            TrialAttemptsRemaining = stored.LicenseInfo.TrialAttemptsRemaining,
                            TrialEndsAt = stored.LicenseInfo.TrialEndsAt,
                            PurchaseDate = stored.LicenseInfo.PurchaseDate,
                            ExpiryDate = stored.LicenseInfo.ExpiryDate,
                            PlanId = stored.LicenseInfo.PlanId,
                            PlanName = stored.LicenseInfo.PlanName
                        };
                        tokenStorage.UpdateUserAndLicense(stored.UserInfo, updated);
                    }
                    ShowAppView();
                    return;
                }
                string message = GetControllerInitFailureMessage(ex);
                bool isLicenseError = IsLicenseValidationFailure(ex);
                string title = isLicenseError ? "License Error" : "Validation Failed";
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                AuthPanel.SetContent(_corporateSignInComponent);
            }
        }

        private void AuthHostView_CloseRequested(object sender, EventArgs e)
        {
            this.Close();
        }

        private void ShowAppView()
        {
            AuthPanel.Visibility = Visibility.Collapsed;
            AppPanel.Visibility = Visibility.Visible;

            // Keep AppData config.toml in sync with the current user's license (from tokens) so native validation uses the right key.
            UpdateConfigWithCurrentUserLicense();

            // Always show home screen when entering app (e.g. after login or re-login)
            ShowDetectionContent();
            ResetAppContentToHome();
            if (TopNavBar != null)
                TopNavBar.SelectedPage = "Home";

            // Initialize stats and license display when showing App view
            StatisticsCardsControl.TotalDetections = "0";
            StatisticsCardsControl.TotalDeepfakes = "0";
            StatisticsCardsControl.TotalAnalysisTime = "0m";
            StatisticsCardsControl.LastDetection = "Never";
            UpdateLicenseDisplay();
            RefreshStatisticsAsync();
            // Deferred refresh so BottomBar sees tokens after file is flushed (avoids No License / 0 days)
            Dispatcher.BeginInvoke(new Action(UpdateLicenseDisplay), DispatcherPriority.ApplicationIdle);

            // Initialize (or re-initialize) native controller with current user's license when entering app.
            // If controller is null (e.g. after logout), init runs and validates this user's key with Keygen
            // for this machine — so only the user whose license is bound to this machine can use detection.
            if (controller == null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    InitializeController();
                }), DispatcherPriority.Loaded);
            }

            // Show system tray when user is logged in (right-click menu: detection agents, Open Results Folder, Version, Exit)
            ShowTray();
        }

        private void ShowAuthView()
        {
            // After logout: go directly to Sign In (skip Welcome and Get Started)
            _signInComponent.ClearInputs();
            AuthPanel.SetContent(_signInComponent);
            AuthPanel.Visibility = Visibility.Visible;
            AppPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>Show Welcome screen (first run / session expired). Use this instead of Sign In when user is effectively logged out.</summary>
        private void ShowWelcomeView()
        {
            AuthPanel.SetContent(_welcomeComponent);
            AuthPanel.Visibility = Visibility.Visible;
            AppPanel.Visibility = Visibility.Collapsed;
            // When showing Welcome again (e.g. restore from tray), restart 7s timer so it auto-advances to Get Started
            _welcomeComponent.RestartTimer();
        }

        /// <summary>Called from App when SessionExpiredException is caught so we show Welcome instead of crashing.</summary>
        public void ShowWelcomeViewIfNeeded()
        {
            Dispatcher.Invoke(ShowWelcomeView);
        }

        /// <summary>Called when session expired from API (legacy name); use ShowWelcomeViewIfNeeded for new callers.</summary>
        public void ShowAuthViewIfNeeded()
        {
            Dispatcher.Invoke(ShowWelcomeView);
        }

        /// <summary>Called when the window is restored from a second-instance launch (e.g. user clicked desktop icon while app was in tray).
        /// If the user was logged out on close (Remember Me false), tokens are cleared; show Welcome screen instead of leaving Home visible.</summary>
        public void EnsureViewMatchesAuthStateAfterRestore()
        {
            try
            {
                var tokenStorage = new TokenStorage();
                var tokens = tokenStorage.GetTokens();
                if (tokens == null || string.IsNullOrWhiteSpace(tokens.RefreshToken))
                    ShowWelcomeView();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureViewMatchesAuthStateAfterRestore: {ex.Message}");
            }
        }

        /// <summary>
        /// On startup with stored tokens: try refresh once. If refresh succeeds, show app view; otherwise clear tokens and show Welcome
        /// so the user gets the first-run flow instead of Sign In (avoids "access token is null" and wrong screen).
        /// </summary>
        private async void TryRestoreSessionAndShowAppAsync(TokenStorage tokenStorage, TokenStorage.StoredTokens storedTokens)
        {
            // No refresh token or no access token (e.g. corrupted file): clear and show Welcome without calling API
            if (string.IsNullOrWhiteSpace(storedTokens.RefreshToken) || string.IsNullOrWhiteSpace(storedTokens.AccessToken))
            {
                tokenStorage.ClearTokens();
                Dispatcher.Invoke(ShowWelcomeView);
                return;
            }
            try
            {
                var authService = new AuthService();
                var refreshResponse = await authService.RefreshTokenAsync(storedTokens.RefreshToken).ConfigureAwait(false);
                if (refreshResponse == null || string.IsNullOrEmpty(refreshResponse.AccessToken))
                {
                    tokenStorage.ClearTokens();
                    Dispatcher.Invoke(ShowWelcomeView);
                    return;
                }
                tokenStorage.UpdateAccessToken(refreshResponse.AccessToken, refreshResponse.ExpiresIn);
                Dispatcher.Invoke(() =>
                {
                    ShowAppView();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TryRestoreSessionAndShowApp: {ex.Message}");
                tokenStorage.ClearTokens();
                Dispatcher.Invoke(ShowWelcomeView);
            }
        }
        
        private void MainWindow_InitializeComObjects(object sender, RoutedEventArgs e)
        {
            // Remove handler to prevent multiple calls
            this.Loaded -= MainWindow_InitializeComObjects;
            
            // Prevent multiple initialization attempts
            if (controllerInitializationAttempted && controller != null)
            {
                System.Diagnostics.Debug.WriteLine("Controller initialization already attempted and succeeded, skipping...");
                return;
            }
            
            // Check if we have a license key before attempting initialization
            // If no license key is available yet, don't attempt initialization
            // This allows retry later when license key becomes available
            var tokenStorage = new TokenStorage();
            var storedTokens = tokenStorage.GetTokens();
            bool hasLicenseKey = storedTokens?.LicenseInfo != null && !string.IsNullOrEmpty(storedTokens.LicenseInfo.Key);
            
            if (!hasLicenseKey)
            {
                System.Diagnostics.Debug.WriteLine("No license key available yet, deferring controller initialization. Will retry when license key is available.");
                return;
            }
            
            // Initialize COM objects on UI thread after window is fully loaded
            // Use Dispatcher to ensure we're on the UI thread
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Don't set flag here - let InitializeController set it only on success
                // This allows retry if initialization fails
                InitializeController();
            }), DispatcherPriority.Loaded);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Update logo based on theme (and when user changes theme in Settings)
            UpdateLogo();
            ThemeManager.ThemeChanged += (_, __) => Dispatcher.BeginInvoke(new Action(UpdateLogo));
            
            // Set exact window size (1000x783)
            this.Width = 1000;
            this.Height = 783;
            
            // Ensure window fits within screen bounds
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            if (this.Width > screenWidth)
            {
                this.Width = screenWidth * 0.95; // 95% of screen width
            }
            
            if (this.Height > screenHeight)
            {
                this.Height = screenHeight * 0.95; // 95% of screen height
            }
            
            // Center window on screen
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = (screenHeight - this.Height) / 2;

            // Show tray icon when application starts
            ShowTray();
            
            // If opened from LaunchWindow with pre-validated controller and tokens, show app view immediately
            if (controller != null && controllerInitializationAttempted)
            {
                var tokenStorage = new TokenStorage();
                var storedTokens = tokenStorage.GetTokens();
                if (storedTokens?.UserInfo != null)
                {
                    var ctrl = controller;
                    _inferenceWarmUpTask = Task.Run(() =>
                    {
                        try
                        {
                            System.Threading.Thread.Sleep(3000);
                            var method = ctrl?.GetType().GetMethod("PrepareInferenceEnvironment", Type.EmptyTypes);
                            if (method != null) method.Invoke(ctrl, null);
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Warm-up: {ex.Message}"); }
                    });
                    ShowAppView();
                    return;
                }
            }

            // When Remember Me was true, tokens persist across exit and system restart. Restore session so user is logged in on next launch.
            var tokenStorageForLaunch = new TokenStorage();
            var tokensForLaunch = tokenStorageForLaunch.GetTokens();
            if (tokensForLaunch?.RememberMe == true && tokensForLaunch.UserInfo != null && !string.IsNullOrWhiteSpace(tokensForLaunch.RefreshToken))
            {
                TryRestoreSessionAndShowAppAsync(tokenStorageForLaunch, tokensForLaunch);
                return;
            }
            // Tokens missing, invalid, or Remember Me was false: clear so we don't auto-login or trigger "access token is null" elsewhere
            if (tokensForLaunch != null && (string.IsNullOrWhiteSpace(tokensForLaunch.RefreshToken) || !tokensForLaunch.RememberMe))
            {
                try { tokenStorageForLaunch.ClearTokens(); } catch { }
            }

            // First run after install or invalid tokens: ensure Welcome screen is shown, not Sign In
            AuthPanel.SetContent(_welcomeComponent);
            AuthPanel.Visibility = Visibility.Visible;
            AppPanel.Visibility = Visibility.Collapsed;

            // Stats and controller init happen when App view is shown (after login), not on initial load when Auth is shown
            if (AppPanel.Visibility == Visibility.Visible)
            {
                StatisticsCardsControl.TotalDetections = "0";
                StatisticsCardsControl.TotalDeepfakes = "0";
                StatisticsCardsControl.TotalAnalysisTime = "0m";
                StatisticsCardsControl.LastDetection = "Never";
                RefreshStatisticsAsync();
                UpdateLicenseDisplay();
                if (controller == null)
                {
                    var tokenStorage = new TokenStorage();
                    var storedTokens = tokenStorage.GetTokens();
                    if (storedTokens?.LicenseInfo != null && !string.IsNullOrEmpty(storedTokens.LicenseInfo.Key))
                    {
                        Dispatcher.BeginInvoke(new Action(() => { InitializeController(); }), DispatcherPriority.Loaded);
                    }
                }
            }
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            // Fetch fresh license from server when user returns so trial attempts are up to date (e.g. after detection)
            if (AppPanel.Visibility == Visibility.Visible)
                RefreshLicenseDisplayAfterDetectionAsync();
        }

        private void MainWindow_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left)
                return;

            // Don't drag if the click is on a button or other interactive control
            if (e.OriginalSource is DependencyObject dep)
            {
                var current = dep;
                while (current != null)
                {
                    if (current is System.Windows.Controls.Button || current is System.Windows.Controls.TextBox ||
                        current is System.Windows.Controls.PasswordBox || current is System.Windows.Controls.ComboBox ||
                        current is System.Windows.Documents.Hyperlink || current is System.Windows.Controls.ListBox ||
                        current is System.Windows.Controls.ListView)
                        return;
                    current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                }
            }

            // Allow drag when clicking anywhere that isn't an interactive control (header, background, labels, etc.)
            this.DragMove();
        }

        /// <summary>
        /// Path to config.toml in per-user AppData. Use this so we can write the license key without admin rights
        /// (install folder e.g. Program Files often requires admin to write).
        /// </summary>
        public static string GetAppDataConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "X-PHY", "X-PHY Deepfake Detector");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, "config.toml");
        }

        /// <summary>
        /// Writes the given license key to config.toml in per-user AppData (no admin required).
        /// Call this with the license key from the login response so native validation uses the correct key.
        /// </summary>
        public static void WriteLicenseKeyToExeConfig(string? licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey)) return;
            string configPath = GetAppDataConfigPath();
            UpdateConfigFileWithLicenseKey(configPath, licenseKey.Trim());
            System.Diagnostics.Debug.WriteLine($"WriteLicenseKeyToExeConfig: config.toml updated at {configPath}");
        }

        /// <summary>
        /// Writes the current user's license key (from stored tokens) to config.toml in AppData.
        /// Fallback when we don't have the key from the sign-in event (e.g. from tokens).
        /// </summary>
        private void UpdateConfigWithCurrentUserLicense()
        {
            try
            {
                var tokenStorage = new TokenStorage();
                var storedTokens = tokenStorage.GetTokens();
                string? licenseKey = storedTokens?.LicenseInfo?.Key;
                if (string.IsNullOrEmpty(licenseKey))
                {
                    System.Diagnostics.Debug.WriteLine("UpdateConfigWithCurrentUserLicense: No license key in tokens, skipping.");
                    return;
                }
                string configPath = GetAppDataConfigPath();
                UpdateConfigFileWithLicenseKey(configPath, licenseKey);
                System.Diagnostics.Debug.WriteLine("UpdateConfigWithCurrentUserLicense: config.toml updated with current user license.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateConfigWithCurrentUserLicense failed: {ex.Message}");
            }
        }

        private static void UpdateConfigFileWithLicenseKey(string configPath, string licenseKey)
        {
            try
            {
                if (string.IsNullOrEmpty(licenseKey))
                {
                    System.Diagnostics.Debug.WriteLine("No license key provided, skipping config update");
                    return;
                }

                string configContent = "";
                
                // Read existing config if it exists
                if (File.Exists(configPath))
                {
                    configContent = File.ReadAllText(configPath);
                }
                else
                {
                    // Create default config content if file doesn't exist. Use absolute path for models so native finds them in the install folder.
                    string exeModelsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models").Replace('\\', '/');
                    configContent = $@"[license]
KEY = """"

[app]
modelDirectory = ""{exeModelsPath}""
optOutOfScreenCapture = false

[voice.generic]
voiceGenericModelIdentifier = ""audio_generic_model_20250102_0""
voiceGenericProbScoreThreshold = 0.5
voiceGenericFakeProportionThreshold = 0.7

[voice.live]
voiceLiveModelIdentifier = ""audio_live_model_20241002_2""
voiceLiveProbScoreThreshold = 0.5
voiceLiveFakeProportionThreshold = 0.7

[video]
videoCaffeDetectionSize = 300
videoMaxNumberFaces = 10
videoRollingWindowExpiryDuration = 30
videoRollingWindowCooldownDuration = 10
videoRollingWindowMinimumAlertSize = 5

[video.generic]
videoGenericModelIdentifier = ""video_generic_model_20250505_0.onnx.encrypted""
videoGenericFakeAndContourThreshold = 0.5
videoGenericMaskThreshold = 0.5
videoGenericProbFakeThreshold = 0.5
videoGenericFakeProportionThreshold = 0.7

[video.live]
videoLiveModelIdentifier = ""video_live_model_20241002_0.onnx.encrypted""
videoLiveFakeAndContourThreshold = 0.5
videoLiveMaskThreshold = 0.5
videoLiveProbFakeThreshold = 0.5
videoLiveFakeProportionThreshold = 0.7
";
                }

                // Update license key in config content
                // Pattern: KEY = "old-key" or KEY = ""old-key""
                var keyPattern = @"KEY\s*=\s*""[^""]*""";
                var newKeyLine = $@"KEY = ""{licenseKey}""";
                
                if (Regex.IsMatch(configContent, keyPattern))
                {
                    // Replace existing KEY line
                    configContent = Regex.Replace(configContent, keyPattern, newKeyLine);
                }
                else
                {
                    // Add KEY line after [license] section
                    var licenseSectionPattern = @"(\[license\]\s*\r?\n)";
                    if (Regex.IsMatch(configContent, licenseSectionPattern))
                    {
                        configContent = Regex.Replace(configContent, licenseSectionPattern, $"$1{newKeyLine}\r\n");
                    }
                    else
                    {
                        // Add [license] section at the beginning
                        configContent = $@"[license]
{newKeyLine}

{configContent}";
                    }
                }

                // Ensure directory exists
                string configDir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // Write updated config
                File.WriteAllText(configPath, configContent);
                System.Diagnostics.Debug.WriteLine($"Updated config.toml with license key at: {configPath}");
                
                // Verify the update was successful
                if (File.Exists(configPath))
                {
                    string verifyContent = File.ReadAllText(configPath);
                    var verifyMatch = Regex.Match(verifyContent, @"KEY\s*=\s*""([^""]+)""");
                    if (verifyMatch.Success)
                    {
                        string writtenKey = verifyMatch.Groups[1].Value;
                        if (writtenKey == licenseKey)
                        {
                            System.Diagnostics.Debug.WriteLine($"✓ Config file verified: License key written correctly");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Config file verification failed: Expected '{licenseKey.Substring(0, Math.Min(10, licenseKey.Length))}...' but found '{writtenKey.Substring(0, Math.Min(10, writtenKey.Length))}...'");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"✗ Config file verification failed: Could not find KEY in config file");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update config.toml with license key: {ex.Message}");
                // Don't throw - continue with initialization even if config update fails
            }
        }

        private void InitializeController(bool forceRetry = false)
        {
            // Prevent multiple initialization attempts unless forced retry
            if (controller != null)
            {
                System.Diagnostics.Debug.WriteLine("Controller already initialized, skipping...");
                return;
            }
            
            if (controllerInitializationAttempted && controller == null && !forceRetry)
            {
                System.Diagnostics.Debug.WriteLine("Controller initialization already attempted and failed, skipping retry...");
                return;
            }
            
            // Reset the flag if forcing retry
            if (forceRetry)
            {
                System.Diagnostics.Debug.WriteLine("Force retry requested, resetting initialization flag...");
                controllerInitializationAttempted = false;
            }
            
            try
            {
                // Get license key from stored tokens (from login)
                string licenseKey = null;
                try
                {
                    var tokenStorage = new TokenStorage();
                    var storedTokens = tokenStorage.GetTokens();
                    if (storedTokens?.LicenseInfo != null && !string.IsNullOrEmpty(storedTokens.LicenseInfo.Key))
                    {
                        licenseKey = storedTokens.LicenseInfo.Key;
                        System.Diagnostics.Debug.WriteLine($"Retrieved license key from stored tokens: {licenseKey.Substring(0, Math.Min(10, licenseKey.Length))}...");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No license key found in stored tokens");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error retrieving license key from tokens: {ex.Message}");
                }

                // Get output directory (similar to tray app)
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string outputDir = Path.Combine(appData, "X-PHY", "X-PHY Deepfake Detector");
                Directory.CreateDirectory(outputDir);

                // Config in AppData so we can write the license key without admin rights (install folder often requires admin).
                string configPath = GetAppDataConfigPath();
                System.Diagnostics.Debug.WriteLine($"Using config.toml at: {configPath}");

                // Ensure exe's models folder exists (native may load from there when modelDirectory is absolute).
                string baseModelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
                if (!Directory.Exists(baseModelsDir))
                    Directory.CreateDirectory(baseModelsDir);

                // Update config.toml with license key from login if available
                if (!string.IsNullOrEmpty(licenseKey))
                {
                    System.Diagnostics.Debug.WriteLine($"Updating config.toml with license key from login...");
                    UpdateConfigFileWithLicenseKey(configPath, licenseKey);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Warning: No license key available from login. Controller may fail to initialize if config.toml has expired license.");
                }

                System.Diagnostics.Debug.WriteLine($"Initializing controller with:");
                System.Diagnostics.Debug.WriteLine($"  OutputDir: {outputDir}");
                System.Diagnostics.Debug.WriteLine($"  ConfigPath: {configPath}");
                System.Diagnostics.Debug.WriteLine($"  ConfigExists: {File.Exists(configPath)}");
                System.Diagnostics.Debug.WriteLine($"  LicenseKeySet: {!string.IsNullOrEmpty(licenseKey)}");
                
                // Verify config file has license key before initializing
                if (File.Exists(configPath))
                {
                    string configContent = File.ReadAllText(configPath);
                    bool hasLicenseKey = configContent.Contains("[license]") && 
                                        Regex.IsMatch(configContent, @"KEY\s*=\s*""[^""]+""");
                    System.Diagnostics.Debug.WriteLine($"  ConfigHasLicenseKey: {hasLicenseKey}");
                    
                    // Extract and log the license key from config (first 10 chars for security)
                    if (hasLicenseKey)
                    {
                        var keyMatch = Regex.Match(configContent, @"KEY\s*=\s*""([^""]+)""");
                        if (keyMatch.Success)
                        {
                            string configLicenseKey = keyMatch.Groups[1].Value;
                            System.Diagnostics.Debug.WriteLine($"  ConfigLicenseKey: {configLicenseKey.Substring(0, Math.Min(10, configLicenseKey.Length))}...");
                        }
                    }
                    
                    if (!hasLicenseKey && !string.IsNullOrEmpty(licenseKey))
                    {
                        System.Diagnostics.Debug.WriteLine("Warning: Config file exists but license key not found, updating...");
                        UpdateConfigFileWithLicenseKey(configPath, licenseKey);
                        // Re-read to verify
                        configContent = File.ReadAllText(configPath);
                        hasLicenseKey = configContent.Contains("[license]") && 
                                       Regex.IsMatch(configContent, @"KEY\s*=\s*""[^""]+""");
                        System.Diagnostics.Debug.WriteLine($"  After update - ConfigHasLicenseKey: {hasLicenseKey}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Warning: Config file does not exist, creating with license key...");
                    if (!string.IsNullOrEmpty(licenseKey))
                    {
                        UpdateConfigFileWithLicenseKey(configPath, licenseKey);
                    }
                }

                System.Diagnostics.Debug.WriteLine("Creating ApplicationControllerWrapper instance...");
                System.Diagnostics.Debug.WriteLine($"  About to call: new ApplicationControllerWrapper(\"{outputDir}\", \"{configPath}\")");
                
                controller = new ApplicationControllerWrapper(outputDir, configPath);
                
                // Only mark as attempted AFTER successful initialization
                // This allows retry if initialization fails
                controllerInitializationAttempted = true;
                
                System.Diagnostics.Debug.WriteLine("Controller initialized successfully");

                // Warm up inference environment so first "Start detection" succeeds.
                // When the app is launched automatically right after installation ("Launch when finished"),
                // files and paths may not be ready yet. Delay briefly so post-install can settle.
                var ctrl = controller;
                _inferenceWarmUpTask = Task.Run(() =>
                {
                    try
                    {
                        System.Threading.Thread.Sleep(3000); // Let post-install settle when launched by installer
                        var method = ctrl.GetType().GetMethod("PrepareInferenceEnvironment", Type.EmptyTypes);
                        if (method != null)
                        {
                            method.Invoke(ctrl, null);
                            System.Diagnostics.Debug.WriteLine("Inference environment prepared (warm-up).");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Inference warm-up: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                // Log detailed error information
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine("CONTROLLER INITIALIZATION FAILED");
                System.Diagnostics.Debug.WriteLine("========================================");
                System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"Exception Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception Type: {ex.InnerException.GetType().FullName}");
                    System.Diagnostics.Debug.WriteLine($"Inner Exception Message: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"Inner Stack trace: {ex.InnerException.StackTrace}");
                    
                    if (ex.InnerException.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Inner-Inner Exception: {ex.InnerException.InnerException.Message}");
                    }
                }
                
                // Log all exception data
                foreach (var key in ex.Data.Keys)
                {
                    System.Diagnostics.Debug.WriteLine($"Exception Data[{key}]: {ex.Data[key]}");
                }
                
                System.Diagnostics.Debug.WriteLine("========================================");
                
                // Set controller to null so we can check for it later
                controller = null;

                if (IsLicenseExpiredFailure(ex))
                {
                    // Don't show pop-up: treat as expired and show Home with disabled detection + Expired status
                    System.Diagnostics.Debug.WriteLine("Controller init failed with license expired; updating stored license to Expired and refreshing UI.");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            var tokenStorage = new TokenStorage();
                            var stored = tokenStorage.GetTokens();
                            if (stored?.LicenseInfo != null)
                            {
                                var updated = new LicenseInfo
                                {
                                    Key = stored.LicenseInfo.Key,
                                    Status = "Expired",
                                    MaxDevices = stored.LicenseInfo.MaxDevices,
                                    TrialAttemptsRemaining = stored.LicenseInfo.TrialAttemptsRemaining,
                                    TrialEndsAt = stored.LicenseInfo.TrialEndsAt,
                                    PurchaseDate = stored.LicenseInfo.PurchaseDate,
                                    ExpiryDate = stored.LicenseInfo.ExpiryDate,
                                    PlanId = stored.LicenseInfo.PlanId,
                                    PlanName = stored.LicenseInfo.PlanName
                                };
                                tokenStorage.UpdateUserAndLicense(stored.UserInfo, updated);
                            }
                            UpdateLicenseDisplay();
                        }
                        catch (Exception updateEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to update license to Expired: {updateEx.Message}");
                        }
                    }), DispatcherPriority.Normal);
                    return;
                }

                // Show error (license) or warning (other); match old app title for license errors
                string userMessage = GetControllerInitFailureMessage(ex);
                bool isLicenseError = IsLicenseValidationFailure(ex);
                string title = isLicenseError ? "License Error" : "Controller Initialization Warning";
                var icon = isLicenseError ? MessageBoxImage.Error : MessageBoxImage.Warning;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(userMessage, title, MessageBoxButton.OK, icon);
                }), DispatcherPriority.Normal);
                // Note: controllerInitializationAttempted is already set to true above,
                // but if forceRetry was used, it will be reset on next call
            }
        }

        /// <summary>True if the exception is from native license validation (Keygen).</summary>
        public static bool IsLicenseValidationFailure(Exception ex)
        {
            string msg = ex?.Message ?? "";
            string lower = msg.ToLowerInvariant();
            return (lower.Contains("maximum") && lower.Contains("machines")) ||
                   lower.Contains("license is invalid") ||
                   lower.Contains("activation unsuccessful") ||
                   (lower.Contains("license") && (lower.Contains("expired") || lower.Contains("not found") || lower.Contains("key is missing"))) ||
                   lower.Contains("license information is invalid") ||
                   lower.Contains("unable to validate license") ||
                   lower.Contains("server returned") ||
                   lower.Contains("could not verify");
        }

        /// <summary>True when the exception indicates the license has expired (Keygen). Use to show expired UI instead of blocking.</summary>
        private static bool IsLicenseExpiredFailure(Exception ex)
        {
            string msg = ex?.Message ?? "";
            string inner = ex?.InnerException?.Message ?? "";
            string lower = (msg + " " + inner).ToLowerInvariant();
            return lower.Contains("license") && lower.Contains("expired");
        }

        /// <summary>
        /// Returns a user-friendly message for controller init failure. Uses the exact native message
        /// for license errors (e.g. machine limit) so the dialog matches the old app.
        /// </summary>
        public static string GetControllerInitFailureMessage(Exception ex)
        {
            string msg = ex?.Message ?? "";
            string lower = msg.ToLowerInvariant();
            // Use the exact message from native for license errors (same as x_phy_detection_program_ui)
            if (lower.Contains("maximum") && lower.Contains("machines"))
                return msg;
            if (lower.Contains("license is invalid"))
                return msg;
            if (lower.Contains("activation unsuccessful"))
                return msg;
            if (lower.Contains("license") && (lower.Contains("expired") || lower.Contains("not found") || lower.Contains("key is missing")))
                return msg;
            if (lower.Contains("license information is invalid") || lower.Contains("unable to validate license"))
                return msg;
            if (lower.Contains("server returned") || lower.Contains("could not verify"))
                return msg;
            return "Failed to initialize detection controller. Some features may not work.\n\n" +
                   "Error: " + msg + "\n\n" +
                   "You can still use the application, but detection will be unavailable. Please check that config.toml exists and required DLLs are present.";
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            // Single-process notification: run background check when minimized to taskbar (user logged in)
            if (WindowState == WindowState.Minimized && notifyIcon != null)
                StartBackgroundProcessCheck();
            else if (WindowState != WindowState.Minimized)
                StopBackgroundProcessCheck();

            // When user restores window from minimized, refresh license so trial attempts show current value
            if (WindowState == WindowState.Normal && AppPanel.Visibility == Visibility.Visible)
                RefreshLicenseDisplayAfterDetectionAsync();

            if (!FloatingWidgetEnabled) return;
            if (WindowState == WindowState.Minimized)
                ShowFloatingWidgetIfDetectionRunning();
            else
                _floatingWidget?.Hide();
        }

        /// <summary>Show floating launcher when detection is running and app is minimized or hidden (from app start or single-process popup).</summary>
        private void ShowFloatingWidgetIfDetectionRunning()
        {
            if (!FloatingWidgetEnabled) return;
            bool windowMinimizedOrHidden = WindowState == WindowState.Minimized || !IsVisible;
            if (!windowMinimizedOrHidden)
            {
                _floatingWidget?.Hide();
                return;
            }
            bool isDetectionRunning = controller != null && controller.IsDetectionRunning();
            if (!isDetectionRunning)
            {
                _floatingWidget?.Hide();
                return;
            }
            if (_floatingWidget == null)
            {
                _floatingWidget = new FloatingWidgetWindow();
                _floatingWidget.SetOwnerWindow(this);
                _floatingWidget.SetActions(FloatingWidget_CancelDetection, FloatingWidget_StopAndViewResults);
            }
            // Only position at bottom-right when (re)showing after being hidden; keep user-dragged position otherwise
            if (!_floatingWidget.IsVisible)
                _floatingWidget.PositionAtBottomRight();
            _floatingWidget.SetDetectionState(true, overallClassification == true);
            _floatingWidget.Show();
        }

        private void FloatingWidget_CancelDetection()
        {
            Dispatcher.Invoke(() =>
            {
                openResultsFolderAfterStop = false;
                StopDetection_Click(null, EventArgs.Empty);
                _floatingWidget?.Hide();
            });
        }

        private void FloatingWidget_StopAndViewResults()
        {
            Dispatcher.Invoke(() =>
            {
                // Use same logic as Detection Notification "Stop & View Results": same path resolution, same hadDeepfake, same result view.
                // Only push once per run: if user already clicked "Stop & View Results" on the notification (or vice versa), skip duplicate save.
                string baseDir = null;
                try { baseDir = controller?.GetResultsDir() ?? ""; } catch { }
                string artifactPath = DetectionResultsLoader.ResolveFullArtifactPath(baseDir ?? "", isAudioDetection);
                if (string.IsNullOrEmpty(artifactPath)) artifactPath = baseDir ?? "Local";
                if (!_resultPushedForStopAndViewResults)
                {
                    _resultPushedForStopAndViewResults = true;
                    PushDetectionResultToBackend(artifactPath, _deepfakeDetectedDuringRun);
                }
                _pathForResultsAfterStop = artifactPath; // so finally block opens this exact result (matches what we saved)
                openResultsFolderAfterStop = true;
                StopDetection_Click(null, EventArgs.Empty);
            });
        }

        private void UpdateLogo()
        {
            if (LogoImage == null) return;

            try
            {
                var isLight = ThemeManager.CurrentTheme == Theme.Light;
                var logoPath = isLight ? "/x-phy-inverted-logo.png" : "/x-phy.png";
                LogoImage.Source = new BitmapImage(new Uri(logoPath, UriKind.Relative));
                System.Diagnostics.Debug.WriteLine($"MainWindow: Set logo to {logoPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Error updating logo - {ex.Message}");
            }
        }


        private async void StartDetectionFromPopup(DetectionSource source, bool isLiveCallMode, bool isAudioMode, bool isRetry = false, string? mediaSourceDisplayName = null)
        {
            if (!isRetry)
                _inferenceEnvRetryCount = 0;

            try
            {
                // If controller is null, try to initialize it first
                if (controller == null)
                {
                    System.Diagnostics.Debug.WriteLine("Controller is null in StartDetectionFromPopup, attempting initialization...");
                    
                    // Check if we have a license key
                    var tokenStorage = new TokenStorage();
                    var storedTokens = tokenStorage.GetTokens();
                    if (storedTokens?.LicenseInfo == null || string.IsNullOrEmpty(storedTokens.LicenseInfo.Key))
                    {
                        MessageBox.Show(
                            "Controller not initialized and no license key found.\n\n" +
                            "Please ensure you are logged in and have a valid license.",
                            "Controller Initialization Error", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
                        return;
                    }
                    
                    // Try to initialize the controller (force retry)
                    InitializeController(forceRetry: true);
                    
                    // Check again after initialization attempt
                    if (controller == null)
                    {
                        MessageBox.Show(
                            "Failed to initialize detection controller.\n\n" +
                            "Please check Debug output for more details.",
                            "Controller Initialization Failed", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
                        return;
                    }
                }

                if (controller.IsDetectionRunning())
                {
                    Dispatcher.Invoke(() =>
                    {
                        ShowAnalyzingScreenWhenDetectionRunning();
                        MessageBox.Show("Detection is already running.", "Information", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                    return;
                }

                // Consume one trial detection attempt (all entry points: Detection Selection, popup, tray)
                // Backend: POST /api/License/detection-attempt decrements User.TrialAttemptsRemaining and should return the new value in the response so we can update UI in real time.
                var licenseService = new LicensePurchaseService();
                var attemptResult = await licenseService.UseDetectionAttemptAsync();
                if (!attemptResult.Allowed)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            attemptResult.Message ?? "You have no detection attempts remaining. Please subscribe to continue.",
                            "Detection Not Allowed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                    return;
                }

                // Update stored license and UI immediately from detection-attempt response (TrialAttemptsRemaining is on User in DB; backend should return it in response)
                if (attemptResult.TrialAttemptsRemaining.HasValue)
                {
                    var tokenStorage = new TokenStorage();
                    var tokens = tokenStorage.GetTokens();
                    if (tokens?.LicenseInfo != null)
                    {
                        var updatedLicense = new LicenseInfo
                        {
                            Key = tokens.LicenseInfo.Key,
                            Status = tokens.LicenseInfo.Status,
                            MaxDevices = tokens.LicenseInfo.MaxDevices,
                            PlanId = tokens.LicenseInfo.PlanId,
                            PlanName = tokens.LicenseInfo.PlanName,
                            TrialEndsAt = tokens.LicenseInfo.TrialEndsAt,
                            PurchaseDate = tokens.LicenseInfo.PurchaseDate,
                            ExpiryDate = tokens.LicenseInfo.ExpiryDate,
                            TrialAttemptsRemaining = attemptResult.TrialAttemptsRemaining
                        };
                        tokenStorage.UpdateUserAndLicense(tokens.UserInfo, updatedLicense);
                    }
                    Dispatcher.Invoke(() => UpdateLicenseDisplay());
                }

                // Hide selection container so only results panel is visible (fix broken view)
                DetectionSelectionContainer.Visibility = Visibility.Collapsed;
                StartDetectionCard.Visibility = Visibility.Collapsed;
                DetectionResultsPanel.Visibility = Visibility.Visible;

                // Default session duration
                int duration = 60;

                // Clear previous results and show detection results section
                Dispatcher.Invoke(() =>
                {
                    overallClassification = null;
                    _deepfakeDetectedDuringRun = false;
                    StartDetectionCard.StatusText = $"Identifying Active Media Sources";
                    DetectionResultsComponent.StartDetection(isAudioMode);
                    string detectionType = isAudioMode ? "Audio" : "Video";
                    ShowNotification("Detection Started",
                        $"{detectionType} detection started for {duration} seconds.",
                        Forms.ToolTipIcon.Info);
                    // Show floating launcher once detection is running (short delay so native has started; visible before first classification)
                    var showLauncherTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
                    {
                        Interval = TimeSpan.FromMilliseconds(150)
                    };
                    showLauncherTimer.Tick += (s, _) =>
                    {
                        ((DispatcherTimer)s).Stop();
                        ShowFloatingWidgetIfDetectionRunning();
                    };
                    showLauncherTimer.Start();
                });

                // Get fresh license (incl. TrialAttemptsRemaining) from server after detection started so attempts update in real time
                _ = RefreshLicenseAfterDetectionStartedAsync(licenseService);

                // Track detection mode and source name for result (Media Source = app name: Zoom, Google Chrome, Mozilla Firefox, etc.)
                isWebSurfingMode = !isLiveCallMode;
                isAudioDetection = isAudioMode;
                isStoppingDetection = false;
                _currentMediaSourceDisplayName = !string.IsNullOrWhiteSpace(mediaSourceDisplayName) ? mediaSourceDisplayName.Trim() : GetMediaSourceDisplayName(source);

                // Wait for inference env warm-up (from controller init) so first-time Start detection succeeds
                if (_inferenceWarmUpTask != null)
                {
                    try
                    {
                        await Task.WhenAny(_inferenceWarmUpTask, Task.Delay(15000));
                    }
                    catch { }
                    _inferenceWarmUpTask = null;
                }

                // Detection flows (all four converge on the same result/notification/save logic):
                // - Video Conference (Live Call): result + face + classification callbacks -> ShowFinalResult on isLast -> notification, PushDetectionResultToBackend, RefreshResultsList.
                // - Video Web Stream: same; Stop & View Results uses _pathForResultsAfterStop so Session Details matches saved record.
                // - Audio Conference: result + voice classification callbacks -> ShowFinalResult on isLast -> notification (no images), same save/refresh.
                // - Audio Web Stream: same. Stop opens results from finally using _pathForResultsAfterStop.
                if (isAudioMode)
                {
                    // Audio Detection
                    if (isLiveCallMode)
                    {
                        // Live Call Audio Detection
                        controller.StartLiveCallAudioDetection(
                            duration,
                            // Result callback
                            (resultPath, isLast) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        if (isLast)
                                        {
                                            ShowFinalResult(resultPath ?? "");
                                            if (isStoppingDetection)
                                            {
                                                isStoppingDetection = false;
                                                if (openResultsFolderAfterStop)
                                                    OpenResultsAndShowSessionDetailAfterStop(!string.IsNullOrEmpty(_pathForResultsAfterStop) ? _pathForResultsAfterStop : (resultPath ?? ""));
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show($"Error handling result: {ex.Message}", "Error",
                                            MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                });
                            },
                            // Voice classification callback (0=Real, 1=Deepfake, 2=Analyzing, 3=Invalid, 4=None)
                            (classification) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    UpdateAudioClassification(classification);
                                });
                            },
                            // Voice graph score callback
                            (score) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    // Update graph score if needed (for future graph visualization)
                                    System.Diagnostics.Debug.WriteLine($"Voice graph score: {score}");
                                });
                            });
                    }
                    else
                    {
                        // Web Surfing Audio Detection
                        controller.StartWebSurfingAudioDetection(
                            duration,
                            // Result callback
                            (resultPath, isLast) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    try
                                    {
                                        if (isLast)
                                        {
                                            ShowFinalResult(resultPath ?? "");
                                            if (isStoppingDetection)
                                            {
                                                isStoppingDetection = false;
                                                if (openResultsFolderAfterStop)
                                                    OpenResultsAndShowSessionDetailAfterStop(!string.IsNullOrEmpty(_pathForResultsAfterStop) ? _pathForResultsAfterStop : (resultPath ?? ""));
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show($"Error handling result: {ex.Message}", "Error",
                                            MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                });
                            },
                            // Voice classification callback
                            (classification) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    UpdateAudioClassification(classification);
                                });
                            },
                            // Voice graph score callback
                            (score) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    System.Diagnostics.Debug.WriteLine($"Voice graph score: {score}");
                                });
                            });
                    }
                }
                else
                {
                    // Video Detection
                    if (isLiveCallMode)
                    {
                        controller.StartLiveCallVideoDetection(
                        duration,
                        // Result callback
                        (resultPath, isLast) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    if (isLast)
                                    {
                                        ShowFinalResult(resultPath ?? "");
                                        if (isStoppingDetection)
                                        {
                                            isStoppingDetection = false;
                                            if (openResultsFolderAfterStop)
                                                OpenResultsAndShowSessionDetailAfterStop(!string.IsNullOrEmpty(_pathForResultsAfterStop) ? _pathForResultsAfterStop : (resultPath ?? ""));
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Error handling result: {ex.Message}", "Error",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            });
                        },
                        // Face update callback
                        (faces) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                UpdateDetectedFaces(faces);
                            });
                        },
                        // Classification callback
                        (isDeepfake) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                UpdateOverallClassification(isDeepfake);
                            });
                        });
                }
                else
                {
                    controller.StartWebSurfingVideoDetection(
                        duration,
                        // Result callback
                        (resultPath, isLast) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    if (isLast)
                                    {
                                        ShowFinalResult(resultPath ?? "");
                                        if (isStoppingDetection)
                                        {
                                            isStoppingDetection = false;
                                            if (openResultsFolderAfterStop)
                                                OpenResultsAndShowSessionDetailAfterStop(!string.IsNullOrEmpty(_pathForResultsAfterStop) ? _pathForResultsAfterStop : (resultPath ?? ""));
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Error handling result: {ex.Message}", "Error",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            });
                        },
                        // Face update callback
                        (faces) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                UpdateDetectedFaces(faces);
                            });
                        },
                        // Classification callback
                        (isDeepfake) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                UpdateOverallClassification(isDeepfake);
                            });
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                bool isInferenceEnvError = message.IndexOf("inference environment", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("setup inference", StringComparison.OrdinalIgnoreCase) >= 0;

                // One-time retry when inference env setup fails (e.g. warm-up still in progress)
                if (isInferenceEnvError && _inferenceEnvRetryCount == 0)
                {
                    _inferenceEnvRetryCount = 1;
                    System.Diagnostics.Debug.WriteLine("Inference environment setup failed, retrying once after 2.5s...");
                    System.Threading.Thread.Sleep(2500);
                    StartDetectionFromPopup(source, isLiveCallMode, isAudioMode, isRetry: true);
                    return;
                }

                MessageBox.Show($"Failed to start detection: {message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                // Return to home so user is not stuck on detection screen
                ResetAppContentToHome();
            }
        }

        /// <summary>
        /// Switches app content to home (Start Detection card visible; detection/selection panels hidden).
        /// Call after login, after stop detection, or when start detection fails.
        /// </summary>
        private void ResetAppContentToHome()
        {
            DetectionResultsComponent?.Reset();
            DetectionResultsPanel.Visibility = Visibility.Collapsed;
            DetectionSelectionContainer.Visibility = Visibility.Collapsed;
            StartDetectionCard.Visibility = Visibility.Visible;
            StartDetectionCard.StatusText = "Ready to start detection";
            isStoppingDetection = false;
            isWebSurfingMode = false;
            isAudioDetection = false;
            overallClassification = null;
            _deepfakeDetectedDuringRun = false;
        }

        /// <summary>
        /// Shows the analyzing screen (DetectionResultsPanel) and hides start card/selection when detection is already running.
        /// Use when user returns to Home tab or clicks Start Detection while detection is running.
        /// </summary>
        private void ShowAnalyzingScreenWhenDetectionRunning()
        {
            DetectionSelectionContainer.Visibility = Visibility.Collapsed;
            StartDetectionCard.Visibility = Visibility.Collapsed;
            DetectionResultsPanel.Visibility = Visibility.Visible;
        }

        private void InitializeStatusTimer()
        {
            statusTimer = new DispatcherTimer();
            statusTimer.Interval = TimeSpan.FromSeconds(1);
            statusTimer.Tick += StatusTimer_Tick;
            statusTimer.Start();
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            if (DetectionResultsComponent != null)
            {
                // Enable Stop whenever detection results panel is visible so user can always return to home
                bool onDetectionScreen = DetectionResultsPanel.Visibility == Visibility.Visible;
                bool isRunning = controller != null && controller.IsDetectionRunning();
                DetectionResultsComponent.SetStopButtonEnabled(isRunning || onDetectionScreen);
            }
            // Update floating widget: show when minimized/hidden and detection running; hide when detection stops; else update ring (green/red)
            if (FloatingWidgetEnabled)
            {
                bool isRunning = controller != null && controller.IsDetectionRunning();
                bool windowMinimizedOrHidden = WindowState == WindowState.Minimized || !IsVisible;
                if (!isRunning)
                    _floatingWidget?.Hide();
                else if (windowMinimizedOrHidden)
                    ShowFloatingWidgetIfDetectionRunning();
                else if (_floatingWidget != null && _floatingWidget.IsVisible)
                    _floatingWidget.SetDetectionState(true, overallClassification == true);
            }
        }

        private void DetectionResultsComponent_DeepfakeDetected(object sender, EventArgs e)
        {
            // Keep floater in sync when deepfake is detected (e.g. audio classification 1); notification uses same state
            if (FloatingWidgetEnabled && _floatingWidget != null && _floatingWidget.IsVisible)
                _floatingWidget.SetDetectionState(true, true);
            ShowDeepfakeNotification();
        }

        private void TopNavBar_NavigationClicked(object sender, string page)
        {
            switch (page)
            {
                case "Home":
                    ShowDetectionContent();
                    if (controller != null && controller.IsDetectionRunning())
                        ShowAnalyzingScreenWhenDetectionRunning();
                    else
                        ResetAppContentToHome();
                    break;
                case "Results":
                    ShowDetectionResultsScreen();
                    break;
                case "Profile":
                    ShowProfileComponent();
                    break;
                case "Settings":
                    ShowSettingsComponent();
                    break;
            }
        }
        
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to log out?", "Logout", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
                DoLogout();
        }

        private void DoLogout()
        {
            try
            {
                // Keep tray running in background; only Exit from tray closes the app
                var tokenStorage = new TokenStorage();
                tokenStorage.ClearTokens();
                controller = null;
                controllerInitializationAttempted = false;
                ShowAuthView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during logout: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BottomBar_SupportClicked(object sender, EventArgs e)
        {
            ShowSupportComponent();
        }

        private void ShowSupportComponent()
        {
            DetectionContentGrid.Visibility = Visibility.Collapsed;
            StatisticsCardsGrid.Visibility = Visibility.Collapsed;
            PlansComponent.Visibility = Visibility.Collapsed;
            StripePaymentComponentContainer.Visibility = Visibility.Collapsed;
            CorpRegisterComponent.Visibility = Visibility.Collapsed;
            SettingsComponent.Visibility = Visibility.Collapsed;
            if (DetectionResultsScreen != null)
                DetectionResultsScreen.Visibility = Visibility.Collapsed;
            if (ProfileComponent != null)
                ProfileComponent.Visibility = Visibility.Collapsed;
            if (LicenseSubscriptionComponent != null)
                LicenseSubscriptionComponent.Visibility = Visibility.Collapsed;
            SupportComponent.Visibility = Visibility.Visible;
            // Clear nav highlight when showing support (opened from footer Support)
            if (TopNavBar != null)
                TopNavBar.SelectedPage = "";
        }

        private void SupportComponent_BackRequested(object sender, EventArgs e)
        {
            ShowDetectionContent();
            SupportComponent.Visibility = Visibility.Collapsed;
        }

        private void ShowProfileComponent()
        {
            DetectionContentGrid.Visibility = Visibility.Collapsed;
            StatisticsCardsGrid.Visibility = Visibility.Collapsed;
            PlansComponent.Visibility = Visibility.Collapsed;
            StripePaymentComponentContainer.Visibility = Visibility.Collapsed;
            CorpRegisterComponent.Visibility = Visibility.Collapsed;
            SupportComponent.Visibility = Visibility.Collapsed;
            if (LicenseSubscriptionComponent != null)
                LicenseSubscriptionComponent.Visibility = Visibility.Collapsed;
            SettingsComponent.Visibility = Visibility.Collapsed;
            if (DetectionResultsScreen != null)
                DetectionResultsScreen.Visibility = Visibility.Collapsed;
            if (TopNavBar != null)
                TopNavBar.SelectedPage = "Profile";
            ProfileComponent.Visibility = Visibility.Visible;
            _ = ProfileComponent.LoadProfileAsync();
        }

        private void ShowSettingsComponent()
        {
            DetectionContentGrid.Visibility = Visibility.Collapsed;
            StatisticsCardsGrid.Visibility = Visibility.Collapsed;
            PlansComponent.Visibility = Visibility.Collapsed;
            StripePaymentComponentContainer.Visibility = Visibility.Collapsed;
            CorpRegisterComponent.Visibility = Visibility.Collapsed;
            SupportComponent.Visibility = Visibility.Collapsed;
            ProfileComponent.Visibility = Visibility.Collapsed;
            if (DetectionResultsScreen != null)
                DetectionResultsScreen.Visibility = Visibility.Collapsed;
            if (TopNavBar != null)
                TopNavBar.SelectedPage = "Settings";
            SettingsComponent.Visibility = Visibility.Visible;
        }

        private void ProfileComponent_BackRequested(object sender, EventArgs e)
        {
            ShowDetectionContent();
            ProfileComponent.Visibility = Visibility.Collapsed;
            if (TopNavBar != null)
                TopNavBar.SelectedPage = "Home";
        }

        private void ProfileComponent_ViewFullDetailsRequested(object sender, EventArgs e)
        {
            ProfileComponent.Visibility = Visibility.Collapsed;
            LicenseSubscriptionComponent.Visibility = Visibility.Visible;
            _ = LicenseSubscriptionComponent.LoadLicensesAsync();
        }

        private void ProfileComponent_ViewPlansRequested(object sender, EventArgs e)
        {
            ProfileComponent.Visibility = Visibility.Collapsed;
            ShowPlansComponent();
        }

        private void LicenseSubscriptionComponent_BackRequested(object sender, EventArgs e)
        {
            LicenseSubscriptionComponent.Visibility = Visibility.Collapsed;
            ShowProfileComponent();
        }

        private void LicenseSubscriptionComponent_UpgradePlanRequested(object sender, EventArgs e)
        {
            LicenseSubscriptionComponent.Visibility = Visibility.Collapsed;
            ShowPlansComponent();
        }

        private void ProfileComponent_ChangePasswordRequested(object sender, EventArgs e)
        {
            _changePasswordDialog.Clear();
            ChangePasswordOverlayContent.Content = _changePasswordDialog;
            ChangePasswordOverlay.Visibility = Visibility.Visible;
        }

        private async void ChangePasswordDialog_UpdatePasswordRequested(object sender, EventArgs e)
        {
            if (!_changePasswordDialog.Validate(out var err))
            {
                _changePasswordDialog.ShowError(err);
                return;
            }
            var tokenStorage = new TokenStorage();
            var tokens = tokenStorage.GetTokens();
            if (tokens?.AccessToken == null)
            {
                _changePasswordDialog.ShowError("Session expired. Please sign in again.");
                return;
            }
            _changePasswordDialog.SetBusy(true);
            _changePasswordDialog.ClearAndHideError();
            try
            {
                await _authServiceForChangePassword.RequestChangePasswordOtpAsync(
                    _changePasswordDialog.CurrentPassword,
                    _changePasswordDialog.NewPassword,
                    tokens.AccessToken);
                _changePasswordCurrent = _changePasswordDialog.CurrentPassword;
                _changePasswordNew = _changePasswordDialog.NewPassword;
                _verifyChangePasswordOtpDialog.Clear();
                ChangePasswordOverlayContent.Content = _verifyChangePasswordOtpDialog;
            }
            catch (Exception ex)
            {
                _changePasswordDialog.ShowError(ex.Message);
            }
            finally
            {
                _changePasswordDialog.SetBusy(false);
            }
        }

        private async void VerifyChangePasswordOtpDialog_VerifyRequested(object sender, EventArgs e)
        {
            var tokenStorage = new TokenStorage();
            var tokens = tokenStorage.GetTokens();
            if (tokens?.AccessToken == null)
            {
                _verifyChangePasswordOtpDialog.ShowError("Session expired. Please sign in again.");
                return;
            }
            _verifyChangePasswordOtpDialog.SetBusy(true);
            _verifyChangePasswordOtpDialog.ShowError(""); // clear
            try
            {
                await _authServiceForChangePassword.VerifyChangePasswordOtpAsync(
                    _verifyChangePasswordOtpDialog.Code,
                    tokens.AccessToken);
                _passwordChangedSuccessDialog.StopCountdown();
                ChangePasswordOverlayContent.Content = _passwordChangedSuccessDialog;
                _passwordChangedSuccessDialog.StartCountdown(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ChangePasswordOverlay.Visibility = Visibility.Collapsed;
                        DoLogout();
                    });
                });
            }
            catch (Exception ex)
            {
                _verifyChangePasswordOtpDialog.ShowError(ex.Message);
            }
            finally
            {
                _verifyChangePasswordOtpDialog.SetBusy(false);
            }
        }

        private async void VerifyChangePasswordOtpDialog_ResendRequested(object sender, EventArgs e)
        {
            var tokenStorage = new TokenStorage();
            var tokens = tokenStorage.GetTokens();
            if (tokens?.AccessToken == null)
            {
                _verifyChangePasswordOtpDialog.ShowError("Session expired. Please sign in again.");
                return;
            }
            if (string.IsNullOrEmpty(_changePasswordCurrent) || string.IsNullOrEmpty(_changePasswordNew))
            {
                _verifyChangePasswordOtpDialog.ShowError("Cannot resend. Please start over from Change Password.");
                return;
            }
            try
            {
                await _authServiceForChangePassword.RequestChangePasswordOtpAsync(
                    _changePasswordCurrent,
                    _changePasswordNew,
                    tokens.AccessToken);
                _verifyChangePasswordOtpDialog.Clear();
                _verifyChangePasswordOtpDialog.ShowError(""); // clear any previous error
                MessageBox.Show("A new code has been sent to your email.", "Code resent", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _verifyChangePasswordOtpDialog.ShowError(ex.Message);
            }
        }

        private void BottomBar_LogoutClicked(object sender, EventArgs e)
        {
            Logout_Click(sender, new RoutedEventArgs());
        }

        private void AuthenticatedApiClient_SessionExpired(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    controller = null;
                    controllerInitializationAttempted = false;
                    ShowWelcomeView();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SessionExpired handler: {ex.Message}");
                }
            });
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // If user chose Exit from tray menu, allow close and shutdown (tokens cleared in ExitFromTray)
            if (_isExitingFromTray)
                return;

            // Close (X): minimize to tray; if Remember Me is false, logout (clear tokens) so next open shows login
            if (notifyIcon != null)
            {
                try
                {
                    var tokenStorage = new TokenStorage();
                    var tokens = tokenStorage.GetTokens();
                    if (tokens == null || !tokens.RememberMe)
                        tokenStorage.ClearTokens();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MainWindow_Closing: {ex.Message}");
                }
                e.Cancel = true;
                Hide();
            }
        }

        private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool visible)
            {
                if (visible)
                {
                    StopBackgroundProcessCheck();
                    if (FloatingWidgetEnabled) _floatingWidget?.Hide();
                }
                else if (notifyIcon != null)
                {
                    StartBackgroundProcessCheck();
                    if (FloatingWidgetEnabled) ShowFloatingWidgetIfDetectionRunning();
                }
            }
        }

        private void StartBackgroundProcessCheck()
        {
            if (_backgroundProcessCheckTimer != null) return;
            // When user minimizes again, allow the next single-process detection to show the notification (reset cooldown for this session)
            _hasSeenZeroProcessesSinceLastPopup = true;
            _backgroundProcessCheckTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(15)
            };
            _backgroundProcessCheckTimer.Tick += BackgroundProcessCheckTimer_Tick;
            _backgroundProcessCheckTimer.Start();
        }

        private void StopBackgroundProcessCheck()
        {
            _backgroundProcessCheckTimer?.Stop();
            _backgroundProcessCheckTimer = null;
        }

        private async void BackgroundProcessCheckTimer_Tick(object? sender, EventArgs e)
        {
            // Run when window is hidden (closed to tray) or minimized to taskbar
            if (notifyIcon == null) return;
            if (IsVisible && WindowState != WindowState.Minimized) return;
            List<DetectedProcess> list = null;
            await Task.Run(() =>
            {
                try
                {
                    var svc = new ProcessDetectionService();
                    list = svc.DetectRelevantProcesses();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Background process check: {ex.Message}");
                }
            }).ConfigureAwait(false);
            if (list == null) return;
            var single = list.Count == 1 ? list[0] : null;
            await Dispatcher.InvokeAsync(() =>
            {
                if (notifyIcon == null) return;
                if (IsVisible && WindowState != WindowState.Minimized) return;
                // Do not show notifications when user is logged out
                try
                {
                    var tokenStorage = new TokenStorage();
                    var tokens = tokenStorage.GetTokens();
                    if (tokens == null) return;
                    // Do not show single/multiple source notifications when license is Expired
                    var status = tokens.LicenseInfo?.Status ?? tokens.UserInfo?.LicenseStatus ?? "";
                    if (status.Trim().Equals("Expired", StringComparison.OrdinalIgnoreCase)) return;
                }
                catch { return; }
                // Do not show single/multiple process notifications when detection is already running
                if (controller != null && controller.IsDetectionRunning()) return;
                // Do not show when user just chose "Stop & View Results" from floating widget (we're about to restore and open results)
                if (openResultsFolderAfterStop) return;

                // When 0 listed processes are running, user closed the app – allow showing popup again when they open one
                if (list.Count == 0)
                {
                    _hasSeenZeroProcessesSinceLastPopup = true;
                    return;
                }
                // Multiple sources: show "Open App To Choose One" notification with 30-min cooldown
                if (list.Count > 1)
                {
                    double elapsed = (DateTime.UtcNow - _lastMultipleSourcesPopupShownAt).TotalSeconds;
                    if (elapsed >= MediaSourcePopupCooldownSeconds)
                    {
                        _lastMultipleSourcesPopupShownAt = DateTime.UtcNow;
                        var multiPopup = new MultipleSourcesDetectedPopup();
                        multiPopup.OpenApplicationRequested += (s, _) =>
                        {
                            Show();
                            WindowState = WindowState.Normal;
                            Activate();
                        };
                        multiPopup.ShowAtBottomRight();
                        _floatingWidget?.BringAboveNotifications();
                    }
                    return;
                }
                if (list.Count != 1 || single == null) return;
                // Do not show if a single-process notification is already open (avoids duplicate popups)
                if (MediaSourceDetectedPopup.IsAnyOpen) return;
                // Show only if: we saw 0 since last popup (close-and-reopen), or 30-min cooldown has passed
                bool cooldownPassed = (DateTime.UtcNow - _lastMediaSourcePopupShownAt).TotalSeconds >= MediaSourcePopupCooldownSeconds;
                if (!_hasSeenZeroProcessesSinceLastPopup && !cooldownPassed) return;
                _lastMediaSourcePopupShownAt = DateTime.UtcNow;
                _hasSeenZeroProcessesSinceLastPopup = false;
                var popup = new MediaSourceDetectedPopup();
                popup.StartDetectionChosen += (s, ev) =>
                {
                    StartDetectionFromPopup(ev.Source, ev.IsLiveCallMode, ev.IsAudioMode);
                    ShowDetectionContent();
                    DetectionSelectionContainer.Visibility = Visibility.Collapsed;
                    DetectionResultsPanel.Visibility = Visibility.Visible;
                    WindowState = WindowState.Minimized;
                    try { ProcessDetectionService.BringProcessWindowToForeground(single.ProcessId); } catch { }
                };
                popup.ShowForProcess(single);
                _floatingWidget?.BringAboveNotifications();
            });
        }

        private void BottomBar_SubscribeClicked(object sender, EventArgs e)
        {
            ShowPlansComponent();
        }

        private void ShowPlansComponent()
        {
            DetectionContentGrid.Visibility = Visibility.Collapsed;
            StatisticsCardsGrid.Visibility = Visibility.Collapsed;
            SupportComponent.Visibility = Visibility.Collapsed;
            SettingsComponent.Visibility = Visibility.Collapsed;
            if (DetectionResultsScreen != null)
                DetectionResultsScreen.Visibility = Visibility.Collapsed;
            if (ProfileComponent != null)
                ProfileComponent.Visibility = Visibility.Collapsed;
            if (LicenseSubscriptionComponent != null)
                LicenseSubscriptionComponent.Visibility = Visibility.Collapsed;
            PlansComponent.Visibility = Visibility.Visible;
            StripePaymentComponentContainer.Visibility = Visibility.Collapsed;
            // Clear nav highlight when showing plans (opened from footer Subscribe)
            if (TopNavBar != null)
                TopNavBar.SelectedPage = "";
        }

        private void ShowDetectionResultsScreen()
        {
            DetectionContentGrid.Visibility = Visibility.Collapsed;
            StatisticsCardsGrid.Visibility = Visibility.Collapsed;
            PlansComponent.Visibility = Visibility.Collapsed;
            StripePaymentComponentContainer.Visibility = Visibility.Collapsed;
            CorpRegisterComponent.Visibility = Visibility.Collapsed;
            SupportComponent.Visibility = Visibility.Collapsed;
            SettingsComponent.Visibility = Visibility.Collapsed;
            if (ProfileComponent != null)
                ProfileComponent.Visibility = Visibility.Collapsed;
            if (LicenseSubscriptionComponent != null)
                LicenseSubscriptionComponent.Visibility = Visibility.Collapsed;
            if (TopNavBar != null)
                TopNavBar.SelectedPage = "Results";

            DetectionResultsScreen.Visibility = Visibility.Visible;
            DetectionResultsScreen.ShowResultsList();

            // Load results from API (or local fallback) so table is populated
            _ = RefreshResultsListFromApiAsync();
        }

        private void ShowDetectionContent()
        {
            // Show detection content and statistics
            DetectionContentGrid.Visibility = Visibility.Visible;
            StatisticsCardsGrid.Visibility = Visibility.Visible;
            // Hide plans, payment, corp register, support, and profile components
            PlansComponent.Visibility = Visibility.Collapsed;
            StripePaymentComponentContainer.Visibility = Visibility.Collapsed;
            CorpRegisterComponent.Visibility = Visibility.Collapsed;
            SupportComponent.Visibility = Visibility.Collapsed;
            SettingsComponent.Visibility = Visibility.Collapsed;
            if (ProfileComponent != null)
                ProfileComponent.Visibility = Visibility.Collapsed;
            if (LicenseSubscriptionComponent != null)
                LicenseSubscriptionComponent.Visibility = Visibility.Collapsed;
            if (DetectionResultsScreen != null)
                DetectionResultsScreen.Visibility = Visibility.Collapsed;
            // Restore Home as selected when returning from Plans/Support (Back to Detection)
            if (TopNavBar != null)
                TopNavBar.SelectedPage = "Home";
        }

        private void PlansComponent_PlanSelected(object sender, PlanSelectedEventArgs e)
        {
            // Hide plans component and statistics
            PlansComponent.Visibility = Visibility.Collapsed;
            StatisticsCardsGrid.Visibility = Visibility.Collapsed;
            SettingsComponent.Visibility = Visibility.Collapsed;
            
            // Create new payment component with selected plan
            var paymentComponent = new StripePaymentComponent(e.Plan);
            paymentComponent.PaymentSuccess += StripePaymentComponent_PaymentSuccess;
            paymentComponent.BackRequested += StripePaymentComponent_BackRequested;
            
            // Set as content of container
            StripePaymentComponentContainer.Content = paymentComponent;
            StripePaymentComponentContainer.Visibility = Visibility.Visible;
        }

        private void PlansComponent_BackRequested(object sender, EventArgs e)
        {
            ShowDetectionContent();
        }

        private void StripePaymentComponent_BackRequested(object sender, EventArgs e)
        {
            // Go back to plans component (keep statistics hidden)
            StripePaymentComponentContainer.Visibility = Visibility.Collapsed;
            PlansComponent.Visibility = Visibility.Visible;
            StatisticsCardsGrid.Visibility = Visibility.Collapsed;
        }

        private void StripePaymentComponent_PaymentSuccess(object sender, PaymentSuccessEventArgs e)
        {
            // Payment component is already showing "Please wait...". Run license refresh and controller re-init, then show the success popup.
            _ = RunPostPurchaseSetupThenShowSuccessPopupAsync(e);
        }

        /// <summary>Runs license refresh and controller re-init, then shows the Payment Success popup. Payment component shows "Please wait..." until this completes.</summary>
        private async System.Threading.Tasks.Task RunPostPurchaseSetupThenShowSuccessPopupAsync(PaymentSuccessEventArgs e)
        {
            try
            {
                await RefreshLicenseFromServerAfterActivationAsync();
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateLicenseDisplay();
                    try
                    {
                        if (controller != null)
                        {
                            controller.Dispose();
                            controller = null;
                        }
                        controllerInitializationAttempted = false;
                        InitializeController(forceRetry: true);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Post-purchase controller re-init: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Post-purchase setup: {ex.Message}");
            }
            finally
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    var popup = PaymentSuccessOverlayContent.Content as Controls.PaymentSuccessPopup;
                    if (popup == null)
                    {
                        popup = new Controls.PaymentSuccessPopup();
                        popup.CloseRequested += PaymentSuccessPopup_CloseRequested;
                        PaymentSuccessOverlayContent.Content = popup;
                    }
                    popup.SetDetails(e.PlanName, e.DurationDays, e.Price, e.PaymentIntentId);
                    StripePaymentComponentContainer.Visibility = Visibility.Collapsed;
                    PaymentSuccessOverlay.Visibility = Visibility.Visible;
                    if (ProfileComponent != null && ProfileComponent.Visibility == Visibility.Visible)
                        _ = ProfileComponent.LoadProfileAsync();
                });
            }
        }

        private void PaymentSuccessPopup_CloseRequested(object sender, EventArgs e)
        {
            PaymentSuccessOverlay.Visibility = Visibility.Collapsed;
            PaymentSuccessOverlayContent.Content = null;
            ShowDetectionContent();
            // Refresh display from current tokens and fetch latest license from server so remaining days update immediately (backend may have ExpiryDate now).
            UpdateLicenseDisplay();
            RefreshLicenseDisplayAfterDetectionAsync();
        }

        private void OpenResultsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (controller != null)
                {
                    controller.OpenResultsFolder();
                }
                else
                {
                    MessageBox.Show("Controller is not initialized. Please restart the application.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open results folder: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartDetectionCard_StartDetectionClicked(object sender, RoutedEventArgs e)
        {
            StartDetection_Click(sender, e);
        }

        private void StartDetection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // If controller is null, try to initialize it first
                if (controller == null)
                {
                    System.Diagnostics.Debug.WriteLine("Controller is null, attempting to initialize before starting detection...");
                    
                    // Check if we have a license key
                    var tokenStorage = new TokenStorage();
                    var storedTokens = tokenStorage.GetTokens();
                    if (storedTokens?.LicenseInfo == null || string.IsNullOrEmpty(storedTokens.LicenseInfo.Key))
                    {
                        MessageBox.Show(
                            "Controller not initialized and no license key found.\n\n" +
                            "Please ensure you are logged in and have a valid license.\n\n" +
                            "If you have a license, try logging out and logging back in.",
                            "Controller Initialization Error", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
                        return;
                    }
                    
                    // Try to initialize the controller (force retry)
                    System.Diagnostics.Debug.WriteLine("Attempting to initialize controller with force retry...");
                    InitializeController(forceRetry: true);
                    
                    // Check again after initialization attempt
                    if (controller == null)
                    {
                        MessageBox.Show(
                            "Failed to initialize detection controller.\n\n" +
                            "Please check:\n" +
                            "1. You are logged in with a valid license\n" +
                            "2. Required DLLs are present in the application directory\n" +
                            "3. config.toml file exists and is accessible\n\n" +
                            "Check Debug output for more details.",
                            "Controller Initialization Failed", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine("Controller initialized successfully, proceeding with detection...");
                }

                if (controller.IsDetectionRunning())
                {
                    ShowAnalyzingScreenWhenDetectionRunning();
                    MessageBox.Show("Detection is already running.", "Information", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Hide StartDetectionCard and detection results; show only Source Selection (no detection page behind it)
                DetectionResultsPanel.Visibility = Visibility.Collapsed;
                StartDetectionCard.Visibility = Visibility.Collapsed;
                DetectionSelectionContainer.Visibility = Visibility.Visible;

                StartDetectionCard.StatusText = $"Identifying Active Media Sources";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start detection selection: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DetectionSelection_StartDetectionRequested(object sender, DetectionSelectionEventArgs e)
        {
            try
            {
                if (e?.SelectedProcess == null || !e.SelectedSource.HasValue)
                    return;
                if (controller == null)
                {
                    MessageBox.Show("Controller is not initialized.", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (controller.IsDetectionRunning())
                {
                    ShowAnalyzingScreenWhenDetectionRunning();
                    MessageBox.Show("Detection is already running.", "Information", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Start detection (attempt consumed and get-license refresh run inside StartDetectionFromPopup)
                StartDetectionFromPopup(e.SelectedSource.Value, e.IsLiveCallMode, e.IsAudioMode, mediaSourceDisplayName: e.SelectedProcess.DisplayName);

                // Hide detection selection container and show results panel
                DetectionSelectionContainer.Visibility = Visibility.Collapsed;
                DetectionResultsPanel.Visibility = Visibility.Visible;

                // Bring the selected app to the foreground first (so it stays visible when we minimize), then minimize our app
                if (e.SelectedProcess.ProcessType == "MediaPlayer")
                    ProcessDetectionService.EnsureMediaPlayerOpenAndForeground(e.SelectedProcess);
                else
                    ProcessDetectionService.BringProcessWindowToForeground(e.SelectedProcess.ProcessId);
                this.WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start detection: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DetectionSelection_CancelRequested(object sender, EventArgs e)
        {
            // Show StartDetectionCard and hide DetectionSelection container
            DetectionSelectionContainer.Visibility = Visibility.Collapsed;
            StartDetectionCard.Visibility = Visibility.Visible;
            StartDetectionCard.StatusText = "Ready to start detection";
        }


        private async void StopDetection_Click(object sender, EventArgs e)
        {
            try
            {
                if (controller != null)
                {
                    if (isAudioDetection)
                    {
                        controller.StopAudioDetection();
                    }
                    else
                    {
                        bool waitForResults = isWebSurfingMode;
                        isStoppingDetection = true;
                        try
                        {
                            controller.StopVideoDetection(waitForResults);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"StopVideoDetection: {ex.Message}");
                            // Still return to home below
                        }

                        if (waitForResults)
                        {
                            int waitTime = 0;
                            int maxWaitTime = 3000;
                            int checkInterval = 100;
                            while (isStoppingDetection && waitTime < maxWaitTime)
                            {
                                await Task.Delay(checkInterval);
                                waitTime += checkInterval;
                            }
                            if (isStoppingDetection)
                            {
                                isStoppingDetection = false;
                                // Native may not invoke result callback on stop — still open Results and Session Details (on UI thread).
                                if (openResultsFolderAfterStop)
                                {
                                    _openedResultsAfterStop = true;
                                    string path = null;
                                    try { path = controller?.GetResultsDir(); } catch { }
                                    var pathCapture = path ?? "";
                                    Dispatcher.Invoke(() => OpenResultsAndShowSessionDetailAfterStop(pathCapture));
                                }
                            }
                            else
                                await Task.Delay(500);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop detection: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (openResultsFolderAfterStop)
                {
                    _openedResultsAfterStop = true;
                    string path = _pathForResultsAfterStop;
                    if (string.IsNullOrEmpty(path)) try { path = controller?.GetResultsDir(); } catch { }
                    _pathForResultsAfterStop = null;
                    var pathCapture = path ?? "";
                    Dispatcher.Invoke(() => OpenResultsAndShowSessionDetailAfterStop(pathCapture));
                }
                if (!_openedResultsAfterStop)
                    ResetAppContentToHome();
                _openedResultsAfterStop = false;
                RefreshLicenseDisplayAfterDetectionAsync();
            }
        }

        /// <summary>Called after detection has started: fetches fresh license (incl. TrialAttemptsRemaining) and updates token storage + UI so attempts show in real time.</summary>
        private async Task RefreshLicenseAfterDetectionStartedAsync(LicensePurchaseService licenseService)
        {
            try
            {
                await Task.Delay(400);
                var result = await licenseService.ValidateLicenseAsync();
                if (result != null && result.Valid && result.License != null)
                {
                    var normalized = NormalizeLicenseFromApi(result.License);
                    var tokenStorage = new TokenStorage();
                    var tokens = tokenStorage.GetTokens();
                    if (tokens != null)
                    {
                        var userInfo = tokens.UserInfo != null ? new UserInfo { Id = tokens.UserInfo.Id, Username = tokens.UserInfo.Username, LicenseStatus = normalized.Status, TrialEndsAt = tokens.UserInfo.TrialEndsAt, UserType = tokens.UserInfo.UserType } : tokens.UserInfo;
                        tokenStorage.UpdateUserAndLicense(userInfo, normalized);
                    }
                    Dispatcher.Invoke(() => UpdateLicenseDisplay());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh license after detection started: {ex.Message}");
            }
        }

        /// <summary>
        /// Called after controller is created (license activated with Keygen). LMS sets expiry at activation time,
        /// so we retrieve the license from LMS again and update tokens/UI with the correct expiry.
        /// Returns a task so callers can await completion.
        /// </summary>
        private async Task RefreshLicenseFromServerAfterActivationAsync()
        {
            try
            {
                await Task.Delay(400); // Brief delay for backend to persist new license; we refresh again when user closes the success popup
                var licenseService = new LicensePurchaseService();
                var result = await licenseService.ValidateLicenseAsync();
                if (result != null && result.Valid && result.License != null)
                {
                    var normalized = NormalizeLicenseFromApi(result.License);
                    var tokenStorage = new TokenStorage();
                    var tokens = tokenStorage.GetTokens();
                    if (tokens != null)
                    {
                        var userInfo = tokens.UserInfo != null ? new UserInfo { Id = tokens.UserInfo.Id, Username = tokens.UserInfo.Username, LicenseStatus = normalized.Status, TrialEndsAt = tokens.UserInfo.TrialEndsAt, UserType = tokens.UserInfo.UserType } : tokens.UserInfo;
                        tokenStorage.UpdateUserAndLicense(userInfo, normalized);
                    }
                    await Dispatcher.InvokeAsync(() => UpdateLicenseDisplay());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh license after activation: {ex.Message}");
            }
        }

        private async void RefreshLicenseDisplayAfterDetectionAsync()
        {
            try
            {
                var licenseService = new LicensePurchaseService();
                var result = await licenseService.ValidateLicenseAsync();
                if (result != null && result.Valid && result.License != null)
                {
                    var normalized = NormalizeLicenseFromApi(result.License);
                    var tokenStorage = new TokenStorage();
                    var tokens = tokenStorage.GetTokens();
                    if (tokens != null)
                    {
                        var userInfo = tokens.UserInfo != null ? new UserInfo { Id = tokens.UserInfo.Id, Username = tokens.UserInfo.Username, LicenseStatus = normalized.Status, TrialEndsAt = tokens.UserInfo.TrialEndsAt, UserType = tokens.UserInfo.UserType } : tokens.UserInfo;
                        tokenStorage.UpdateUserAndLicense(userInfo, normalized);
                    }
                    Dispatcher.Invoke(() => UpdateLicenseDisplay());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh license after detection: {ex.Message}");
            }
        }

        private void BackToHome_Click(object sender, EventArgs e)
        {
            try
            {
                ResetAppContentToHome();
                RefreshLicenseDisplayAfterDetectionAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to return to home: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDetectedFaces(DetectedFace[] faces)
        {
            if (faces == null || faces.Length == 0) return;
            DetectionResultsPanel.Visibility = Visibility.Visible;
            DetectionResultsComponent?.UpdateDetectedFaces(faces, GetResourceBrush);
        }

        private void UpdateOverallClassification(bool isDeepfake)
        {
            overallClassification = isDeepfake;
            if (isDeepfake) _deepfakeDetectedDuringRun = true;
            DetectionResultsComponent?.UpdateOverallClassification(isDeepfake);
            // Update floating widget immediately so arc turns red when deepfake is detected (don't wait for status timer)
            if (FloatingWidgetEnabled && _floatingWidget != null && _floatingWidget.IsVisible)
                _floatingWidget.SetDetectionState(true, isDeepfake);
        }

        private void UpdateAudioClassification(int classification)
        {
            if (isAudioDetection)
                System.Diagnostics.Debug.WriteLine($"Audio classification from native: {classification} (0=Real, 1=Deepfake, 2=Analyzing, 3=Invalid, 4=None)");
            switch (classification)
            {
                case 0: overallClassification = false; break;
                case 1: overallClassification = true; _deepfakeDetectedDuringRun = true; break;
                default: overallClassification = null; break;
            }
            DetectionResultsComponent?.UpdateAudioClassification(classification);
            // Update floating widget immediately so arc turns red when audio deepfake is detected (same as video)
            if (FloatingWidgetEnabled && _floatingWidget != null && _floatingWidget.IsVisible)
                _floatingWidget.SetDetectionState(true, overallClassification == true);
        }

        private void ShowFinalResult(string resultPath)
        {
            // Tell floater detection has ended so it stops the arc and closes the Detection Activity popup if open
            if (FloatingWidgetEnabled && _floatingWidget != null && _floatingWidget.IsVisible)
                _floatingWidget.SetDetectionState(false, _deepfakeDetectedDuringRun);

            DetectionResultsPanel.Visibility = Visibility.Visible;
            string displayPath = resultPath;
            if (string.IsNullOrEmpty(displayPath))
            {
                try { displayPath = controller?.GetResultsDir() ?? ""; } catch { }
            }
            // When path is the base results dir (e.g. .../Deepfake - results), resolve to full run folder (.../video/DD-MM-YYYY/HH-mm) so DB and images use correct path.
            displayPath = DetectionResultsLoader.ResolveFullArtifactPath(displayPath ?? "", isAudioDetection) ?? displayPath;
            DetectionResultsComponent?.ShowFinalResult(displayPath ?? resultPath);

            int faceCount = DetectionResultsComponent?.DetectedFacesCount ?? 0;
            // Single source of truth for notification, result view, and DB (no mismatch)
            bool hadDeepfake = _deepfakeDetectedDuringRun;
            if (hadDeepfake)
            {
                int rawConfidence = DetectionResultsComponent?.LastConfidencePercent ?? 0;
                int confidence = GetDisplayConfidence(rawConfidence, true);
                var evidenceImage = isAudioDetection ? null : DetectionResultsComponent?.LatestEvidenceImage;
                ShowDetectionCompletedWithThreatNotification(displayPath ?? "", confidence, evidenceImage, isAudioDetection);
            }
            else
            {
                ShowDetectionCompletedNotification(isAudioDetection ? "Audio: NOT DETECTED" : "NOT DETECTED", displayPath ?? "", isAudioDetection);
            }

            if (isStoppingDetection)
            {
                isStoppingDetection = false;
                System.Diagnostics.Debug.WriteLine("ShowFinalResult: Final result shown, stopping flag cleared");
            }

            // Save detection result to backend (CreateResult) only when detection is completed or stopped.
            // Skip if we already saved when user clicked "Stop & View Results" (avoid duplicate).
            if (!_resultPushedForStopAndViewResults)
            {
                System.Diagnostics.Debug.WriteLine($"ShowFinalResult: Pushing result to backend (audio={isAudioDetection}, hadDeepfake={hadDeepfake})");
                PushDetectionResultToBackend(displayPath ?? resultPath ?? "Local", hadDeepfake);
            }
            _resultPushedForStopAndViewResults = false;
        }

        /// <summary>Confidence to show in notification, result view, and API. When raw is 0 (e.g. audio) but deepfake was detected, use fallback so all show the same value.</summary>
        private static int GetDisplayConfidence(int rawPercent, bool hadDeepfake)
        {
            if (rawPercent > 0) return Math.Min(100, Math.Max(0, rawPercent));
            return hadDeepfake ? 97 : 0;
        }

        /// <summary>Navigate to Results tab, show Session Details for the given result path, and bring main window to front. Used by "Stop & View Results" and by "View Results" on detection completion.</summary>
        private void NavigateToResultsAndShowSessionDetail(string resultPath)
        {
            string resultsDir = null;
            try { resultsDir = controller?.GetResultsDir(); } catch { }
            resultsDir = resultsDir ?? DetectionResultsLoader.GetDefaultResultsDir();
            // When path is the base results dir, resolve to full run folder (.../video/DD-MM-YYYY/HH-mm) so Session Details can load images.
            string artifactPath = DetectionResultsLoader.ResolveFullArtifactPath(resultPath ?? resultsDir ?? "", isAudioDetection);
            if (string.IsNullOrEmpty(artifactPath)) artifactPath = resultPath ?? resultsDir ?? "";
            int rawConfidence = DetectionResultsComponent?.LastConfidencePercent ?? 0;
            var item = new DetectionResultItem
            {
                Timestamp = DateTime.Now,
                Type = isAudioDetection ? "Audio" : "Video",
                IsAiManipulationDetected = _deepfakeDetectedDuringRun,
                ConfidencePercent = GetDisplayConfidence(rawConfidence, _deepfakeDetectedDuringRun),
                ResultPathOrId = artifactPath,
                MediaSourceDisplay = _currentMediaSourceDisplayName ?? "Local",
                SerialNumber = 0,
                DurationSeconds = (decimal)(DetectionResultsComponent?.GetDetectionDurationSeconds() ?? 0)
            };
            ShowDetectionResultsScreen();
            DetectionResultsScreen?.ShowSessionDetailForResult(item, resultsDir);
            // Bring main window to front so user sees the Results page (notification may have been on top).
            try
            {
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
                Activate();
                Topmost = true;
                Topmost = false;
            }
            catch { }
        }

        /// <summary>Called when "Stop & View Results" was used: navigate to Results tab and show Session Details for the just-saved result. Do not clear _resultPushedForStopAndViewResults here; ShowFinalResult clears it after skipping the duplicate push.</summary>
        private void OpenResultsAndShowSessionDetailAfterStop(string resultPath)
        {
            openResultsFolderAfterStop = false;
            _openedResultsAfterStop = true;
            NavigateToResultsAndShowSessionDetail(resultPath);
        }

        /// <summary>Calls CreateResult API with current detection outcome so result is saved in DB and appears in Results tab.</summary>
        private void PushDetectionResultToBackend(string artifactPath, bool aiManipulationDetected)
        {
            try
            {
                int rawConfidence = DetectionResultsComponent?.LastConfidencePercent ?? 0;
                int confidence = GetDisplayConfidence(rawConfidence, aiManipulationDetected);
                double durationSec = DetectionResultsComponent?.GetDetectionDurationSeconds() ?? 60;
                string machineFingerprint = null;
                try { machineFingerprint = new DeviceFingerprintService().GetDeviceFingerprint(); } catch { }
                var request = new CreateResultRequest
                {
                    Timestamp = DateTime.UtcNow,
                    Type = isAudioDetection ? "Audio" : "Video",
                    Outcome = aiManipulationDetected ? "AI Manipulation Detected" : "No AI Manipulation detected",
                    DetectionConfidence = (decimal)Math.Min(100, Math.Max(0, confidence)),
                    MediaSource = _currentMediaSourceDisplayName ?? "Local", // App name (Zoom, Google Chrome)
                    ArtifactPath = string.IsNullOrEmpty(artifactPath) || artifactPath == "Local" ? null : artifactPath, // Path for loading evidence images
                    MachineFingerprint = machineFingerprint,
                    Duration = (decimal)durationSec
                };
#pragma warning disable CS4014
                Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        var response = await _resultsApiService.CreateResultAsync(request);
                        if (response != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"CreateResult succeeded: Id={response.Id}");
                            RefreshStatisticsAsync();
                            // Refresh results list so the table shows the new record when user goes back from Session Details
                            await RefreshResultsListFromApiAsync();
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("CreateResult: no response (e.g. not logged in or API error)");
                            Dispatcher.Invoke(() => ShowResultSaveFailureMessage());
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CreateResult failed: {ex.Message}");
                        Dispatcher.Invoke(() => ShowResultSaveFailureMessage());
                    }
                });
#pragma warning restore CS4014
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PushDetectionResultToBackend: {ex.Message}");
                try { ShowResultSaveFailureMessage(); } catch { }
            }
        }

        // Notification helper
        private Forms.NotifyIcon notifyIcon;
        private Forms.ContextMenuStrip _trayContextMenu;
        
        private void InitializeLicenseManager()
        {
            licenseManager = new LicenseManager();
            UpdateLicenseDisplay();
        }

        /// <summary>Returns display name for "Media Source" (e.g. Zoom, Google Chrome) from the selected detection source.</summary>
        private static string GetMediaSourceDisplayName(DetectionSource source)
        {
            switch (source)
            {
                case DetectionSource.ZoomConferenceVideo:
                case DetectionSource.ZoomConferenceAudio:
                    return "Zoom";
                case DetectionSource.VLCWebStreamVideo:
                case DetectionSource.VLCWebStreamAudio:
                    return "VLC Media Player";
                case DetectionSource.YouTubeWebStreamVideo:
                case DetectionSource.YouTubeWebStreamAudio:
                    return "Google Chrome"; // YouTube is typically in browser
                default:
                    return "Local";
            }
        }

        /// <summary>When the backend returns Trial but the license has actually expired (by date or LMS says Expired), normalize to Expired and 0 attempts so we never overwrite correct state with stale DB values.</summary>
        private static LicenseInfo? NormalizeLicenseFromApi(LicenseInfo? license)
        {
            if (license == null) return null;
            bool isExpired = license.Status?.Trim().Equals("Expired", StringComparison.OrdinalIgnoreCase) == true
                || (license.ExpiryDate.HasValue && license.ExpiryDate.Value.ToUniversalTime() < DateTime.UtcNow)
                || (license.TrialEndsAt.HasValue && license.TrialEndsAt.Value.ToUniversalTime() < DateTime.UtcNow);
            if (!isExpired) return license;
            return new LicenseInfo
            {
                Key = license.Key,
                Status = "Expired",
                MaxDevices = license.MaxDevices,
                PlanId = license.PlanId,
                PlanName = license.PlanName,
                TrialEndsAt = license.TrialEndsAt,
                PurchaseDate = license.PurchaseDate,
                ExpiryDate = license.ExpiryDate,
                TrialAttemptsRemaining = 0
            };
        }

        /// <summary>Call after purchase or when returning to MainWindow so the bottom bar shows current license status.</summary>
        public void RefreshLicenseDisplay()
        {
            UpdateLicenseDisplay();
        }

        /// <summary>Shows a brief message when detection result could not be saved to the server (so user knows why the record is missing from the table).</summary>
        private void ShowResultSaveFailureMessage()
        {
            try
            {
                MessageBox.Show(
                    "The detection result could not be saved to the server. It will not appear in the Results table.\n\nPlease check that you are logged in and your connection is working, then try again.",
                    "Result Not Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch { }
        }

        private void OnBackToResultsListRequested(object sender, EventArgs e)
        {
            _ = RefreshResultsListFromApiAsync();
        }

        /// <summary>Load results list from API (or local fallback) and update the Results table. Call after CreateResult succeeds so the new record appears.</summary>
        private async Task RefreshResultsListFromApiAsync()
        {
            string resultsDir = null;
            try { if (controller != null) resultsDir = controller.GetResultsDir(); } catch { }
            var resultsDirCapture = resultsDir ?? DetectionResultsLoader.GetDefaultResultsDir();
            try
            {
                var response = await _resultsApiService.GetResultsAsync().ConfigureAwait(false);
                Dispatcher.Invoke(() =>
                {
                    if (response?.Results != null)
                        DetectionResultsScreen.SetResultsFromApi(response.Results);
                    else
                        DetectionResultsScreen.SetResultsFromApi(Array.Empty<ResultDto>());
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshResultsListFromApi: {ex.Message}");
                try
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        try { DetectionResultsScreen.SetResultsDirectoryAndRefresh(resultsDirCapture); }
                        catch (Exception ex2) { System.Diagnostics.Debug.WriteLine($"SetResultsDirectoryAndRefresh: {ex2.Message}"); }
                    });
                }
                catch { }
            }
        }

        /// <summary>Load statistics from API and update the Statistics Cards (Total Detections, Total Deepfakes, Total Analysis Time, Last Detection).</summary>
        private async void RefreshStatisticsAsync()
        {
            if (StatisticsCardsControl == null) return;
            try
            {
                var stats = await _resultsApiService.GetStatisticsAsync().ConfigureAwait(false);
                if (stats != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatisticsCardsControl.TotalDetections = stats.TotalDetections.ToString();
                        StatisticsCardsControl.TotalDeepfakes = stats.TotalDeepfakes.ToString();
                        StatisticsCardsControl.TotalAnalysisTime = stats.TotalAnalysisTimeFormatted ?? "0m";
                        StatisticsCardsControl.LastDetection = stats.LastDetectionTimeAgo ?? "Never";
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshStatistics: {ex.Message}");
            }
        }

        private async void UpdateLicenseDisplay()
        {
            try
            {
                var tokenStorage = new TokenStorage();
                var storedTokens = tokenStorage.GetTokens();
                
                if (BottomBar == null) return;

                // Show "Add corp user" button only for Admin
                if (TopNavBar != null)
                    TopNavBar.IsAdmin = string.Equals(storedTokens?.UserInfo?.UserType, "Admin", StringComparison.OrdinalIgnoreCase);
                
                // If controller is not initialized but we now have a license key, try to initialize it
                // This handles the case where license info becomes available after MainWindow was loaded
                if (controller == null && storedTokens?.LicenseInfo != null && !string.IsNullOrEmpty(storedTokens.LicenseInfo.Key))
                {
                    System.Diagnostics.Debug.WriteLine("License key available in UpdateLicenseDisplay, attempting controller initialization...");
                    // Use Dispatcher to ensure we're on UI thread (fire-and-forget)
#pragma warning disable CS4014
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        InitializeController();
                    }), DispatcherPriority.Normal);
#pragma warning restore CS4014
                }
                
                if (storedTokens?.UserInfo != null)
                {
                    var userInfo = storedTokens.UserInfo;
                    var licenseInfo = storedTokens.LicenseInfo;
                    // Prefer Expired when LMS says so (license or user); never show Trial when license is Expired.
                    bool isExpiredFromLms = licenseInfo?.Status?.Trim().Equals("Expired", StringComparison.OrdinalIgnoreCase) == true
                        || userInfo.LicenseStatus?.Trim().Equals("Expired", StringComparison.OrdinalIgnoreCase) == true;
                    string status = isExpiredFromLms ? "Expired" : (licenseInfo?.Status ?? (!string.IsNullOrEmpty(userInfo.LicenseStatus) ? userInfo.LicenseStatus : "Trial"));
                    int daysRemaining = 0;

                    // Only show remaining days when we have ExpiryDate from backend; do not derive from plan name.
                    var expiryForDays = licenseInfo?.ExpiryDate ?? (status.Equals("Trial", StringComparison.OrdinalIgnoreCase) ? userInfo.TrialEndsAt : null) ?? licenseInfo?.TrialEndsAt;
                    if (expiryForDays.HasValue)
                        daysRemaining = Math.Max(0, (int)Math.Ceiling((expiryForDays.Value - DateTime.UtcNow).TotalDays));

                    bool isCorpUser = string.Equals(userInfo.UserType, "Corp", StringComparison.OrdinalIgnoreCase);

                    if (status.Equals("Trial", StringComparison.OrdinalIgnoreCase))
                    {
                        BottomBar.Status = "Trial";
                        BottomBar.RemainingDays = daysRemaining;
                        BottomBar.Attempts = licenseInfo?.TrialAttemptsRemaining;
                        BottomBar.ShowSubscribeButton = true;
                        BottomBar.ShowContactAdminButton = false;
                        if (StartDetectionCard != null) { StartDetectionCard.IsLicenseExpired = false; StartDetectionCard.StatusText = "Ready to start detection"; }
                    }
                    else if (licenseInfo != null && status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    {
                        BottomBar.Status = "Active";
                        BottomBar.RemainingDays = daysRemaining;
                        BottomBar.Attempts = null;
                        BottomBar.ShowSubscribeButton = false;
                        BottomBar.ShowContactAdminButton = false;
                        if (StartDetectionCard != null) { StartDetectionCard.IsLicenseExpired = false; StartDetectionCard.StatusText = "Ready to start detection"; }
                    }
                    else
                    {
                        BottomBar.Status = "Expired";
                        BottomBar.RemainingDays = 0;
                        BottomBar.Attempts = null;
                        if (isCorpUser)
                        {
                            BottomBar.ShowSubscribeButton = false;
                            BottomBar.ShowContactAdminButton = true;
                        }
                        else
                        {
                            BottomBar.ShowSubscribeButton = true;
                            BottomBar.ShowContactAdminButton = false;
                        }
                        if (StartDetectionCard != null)
                        {
                            StartDetectionCard.IsLicenseExpired = true;
                            StartDetectionCard.StatusText = "Subscribe To Start Detection";
                        }
                    }
                }
                else
                {
                    BottomBar.Status = "No License";
                    BottomBar.RemainingDays = 0;
                    BottomBar.Attempts = null;
                    BottomBar.ShowSubscribeButton = true;
                    BottomBar.ShowContactAdminButton = false;
                    if (StartDetectionCard != null) StartDetectionCard.IsLicenseExpired = true;
                }
                if (TopNavBar != null && storedTokens == null)
                    TopNavBar.IsAdmin = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating license display: {ex.Message}");
                // Default to no license if error
                if (BottomBar != null)
                {
                    BottomBar.Status = "No License";
                    BottomBar.RemainingDays = 0;
                    BottomBar.ShowSubscribeButton = true;
                    BottomBar.ShowContactAdminButton = false;
                }
                if (StartDetectionCard != null) StartDetectionCard.IsLicenseExpired = true;
                if (TopNavBar != null)
                    TopNavBar.IsAdmin = false;
            }
        }

        private void TopNavBar_AddCorpUserClicked(object sender, EventArgs e)
        {
            if (TopNavBar != null)
                TopNavBar.SelectedPage = "CorpUser";
            DetectionContentGrid.Visibility = Visibility.Collapsed;
            StatisticsCardsGrid.Visibility = Visibility.Collapsed;
            PlansComponent.Visibility = Visibility.Collapsed;
            StripePaymentComponentContainer.Visibility = Visibility.Collapsed;
            SettingsComponent.Visibility = Visibility.Collapsed;
            if (DetectionResultsScreen != null)
                DetectionResultsScreen.Visibility = Visibility.Collapsed;
            CorpRegisterComponent.ClearInputs();
            CorpRegisterComponent.Visibility = Visibility.Visible;
        }

        private void CorpRegisterComponent_BackRequested(object sender, EventArgs e)
        {
            CorpRegisterComponent.Visibility = Visibility.Collapsed;
            if (TopNavBar != null)
                TopNavBar.SelectedPage = "Home";
            ShowDetectionContent();
        }

        private void CorpRegisterComponent_CorpAccountCreated(object sender, EventArgs e)
        {
            CorpRegisterComponent.Visibility = Visibility.Collapsed;
            if (TopNavBar != null)
                TopNavBar.SelectedPage = "Home";
            ShowDetectionContent();
        }

        /// <summary>Show system tray icon. Right-click shows menu (Open Application, Open Result Directory, Exit).</summary>
        private void ShowTray()
        {
            if (notifyIcon != null) return;
            notifyIcon = new Forms.NotifyIcon();
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    using (var ico = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                    {
                        if (ico != null)
                            notifyIcon.Icon = (System.Drawing.Icon)ico.Clone();
                    }
                }
            }
            catch { }
            if (notifyIcon.Icon == null)
                notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            notifyIcon.Text = "X-PHY Deepfake Detector";
            notifyIcon.Visible = true;
            notifyIcon.BalloonTipClicked += (s, e) => { notifyIcon.Visible = true; };

            // Assign context menu so right-click shows it (reliable on all Windows)
            _trayContextMenu = CreateTrayContextMenu();
            notifyIcon.ContextMenuStrip = _trayContextMenu;
        }

        /// <summary>Hide and dispose tray icon when user logs out.</summary>
        private void HideTray()
        {
            if (notifyIcon == null) return;
            StopBackgroundProcessCheck();
            try
            {
                notifyIcon.ContextMenuStrip = null;
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            catch { }
            notifyIcon = null;
            try
            {
                _trayContextMenu?.Dispose();
            }
            catch { }
            _trayContextMenu = null;
        }

        /// <summary>Create the right-click context menu: Open Application, Open Result Directory, Exit.</summary>
        private Forms.ContextMenuStrip CreateTrayContextMenu()
        {
            var menu = new Forms.ContextMenuStrip();
            menu.BackColor = System.Drawing.Color.White;
            menu.ForeColor = System.Drawing.Color.FromArgb(0x1A, 0x1A, 0x1A);
            menu.Font = new System.Drawing.Font("Segoe UI", 11f);
            menu.Padding = new Forms.Padding(6, 8, 6, 8);
            menu.Renderer = new TrayMenuNotificationStyleRenderer();

            menu.Items.Add("Open Application", null, (_, __) => OpenApplicationFromTray());
            menu.Items.Add("Open Result Directory", null, (_, __) => OpenResultsFolderFromTray());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, __) => ExitFromTray());
            return menu;
        }

        private void OpenApplicationFromTray()
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                // If user closed from Auth view (no tokens), show Launch (Get Started) when opening from tray
                try
                {
                    var tokenStorage = new TokenStorage();
                    var tokens = tokenStorage.GetTokens();
                    if (tokens == null)
                    {
                        AuthPanel.SetContent(_launchComponent);
                        AuthPanel.Visibility = Visibility.Visible;
                        AppPanel.Visibility = Visibility.Collapsed;
                        return;
                    }
                }
                catch { }
            });
        }

        private static string GetAppVersion()
        {
            try
            {
                var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (ver != null) return $"{ver.Major}.{ver.Minor}.{ver.Build}";
            }
            catch { }
            return "1.0.10";
        }

        /// <summary>Start detection from tray menu; invokes native controller on UI thread.</summary>
        private void TrayStartDetection(DetectionSource source, bool isLiveCallMode, bool isAudioMode)
        {
            Dispatcher.Invoke(() =>
            {
                StartDetectionFromPopup(source, isLiveCallMode, isAudioMode);
            });
        }

        private void OpenResultsFolderFromTray()
        {
            try
            {
                string path = null;
                if (controller != null)
                    path = controller.GetResultsDir();
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                    path = DetectionResultsLoader.GetDefaultResultsDir();
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    Process.Start("explorer.exe", path);
                else
                    MessageBox.Show("Results folder not found.", "X-PHY", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open results folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Exit from tray: close the application. Clear tokens only when Remember Me was false so user stays logged in next launch when they had checked Remember Me.</summary>
        private void ExitFromTray()
        {
            StopBackgroundProcessCheck();
            try
            {
                var tokenStorage = new TokenStorage();
                var tokens = tokenStorage.GetTokens();
                if (tokens == null || !tokens.RememberMe)
                    tokenStorage.ClearTokens();
            }
            catch { }
            HideTray();
            controller = null;
            _isExitingFromTray = true;
            Dispatcher.Invoke(Close);
        }

        private void InitializeNotifications()
        {
            // Tray is initialized in InitializeTray(). Balloon/toast use DetectionNotificationWindow.
            if (notifyIcon != null)
                notifyIcon.BalloonTipClicked += (s, e) => { /* keep visible */ };
        }
        
        private void ShowNotification(string title, string message, Forms.ToolTipIcon icon)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DetectionNotificationWindow.CloseAllOpen();
                var popup = new DetectionNotificationWindow();
                popup.SetContent(title ?? "", message ?? "");
                popup.ShowAtBottomRight(autoCloseSeconds: 5);
                _floatingWidget?.BringAboveNotifications();
            }));
        }

        private void ShowDeepfakeNotification()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DetectionNotificationWindow.CloseAllOpen();
                string resultPath = "";
                try { resultPath = controller?.GetResultsDir() ?? ""; } catch { }
                int rawConfidence = DetectionResultsComponent?.LastConfidencePercent ?? 0;
                int confidence = GetDisplayConfidence(rawConfidence, true);
                var evidenceImage = isAudioDetection ? null : DetectionResultsComponent?.LatestEvidenceImage;

                var popup = new DetectionNotificationWindow();
                popup.SetDeepfakeContent(
                    confidence,
                    resultPath,
                    openResultsFolder: () =>
                    {
                        try { if (controller != null) controller.OpenResultsFolder(); }
                        catch (Exception ex) { MessageBox.Show($"Failed to open results folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
                    },
                    stopDetectionAndOpenResults: () =>
                    {
                        // Same as floating launcher: resolve path, push to DB, set path for results view. Only push once per run (avoid duplicate if user also used floater).
                        string baseDir = resultPath;
                        if (string.IsNullOrEmpty(baseDir)) try { baseDir = controller?.GetResultsDir() ?? ""; } catch { }
                        string artifactPath = DetectionResultsLoader.ResolveFullArtifactPath(baseDir ?? "", isAudioDetection);
                        if (string.IsNullOrEmpty(artifactPath)) artifactPath = baseDir ?? "Local";
                        if (!_resultPushedForStopAndViewResults)
                        {
                            _resultPushedForStopAndViewResults = true;
                            PushDetectionResultToBackend(artifactPath, true);
                        }
                        _pathForResultsAfterStop = artifactPath;
                        openResultsFolderAfterStop = true;
                        StopDetection_Click(null, EventArgs.Empty);
                    },
                    evidenceImageLeft: evidenceImage,
                    evidenceImageRight: null,
                    isAudio: isAudioDetection);
                popup.ShowAtBottomRight(autoCloseSeconds: 4);
                _floatingWidget?.BringAboveNotifications();
            }));
        }

        private void ShowDetectionCompletedNotification(string message, string resultPath, bool isAudio = false)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DetectionNotificationWindow.CloseAllOpen();
                var popup = new DetectionNotificationWindow();
                popup.SetDetectionCompletedContent(message, resultPath,
                    openResultsFolder: () =>
                    {
                        try
                        {
                            if (controller != null) controller.OpenResultsFolder();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to open results folder: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    },
                    navigateToResultsPage: () => NavigateToResultsAndShowSessionDetail(resultPath),
                    isAudio: isAudio);
                popup.ShowAtBottomRight(autoCloseSeconds: 0);
                _floatingWidget?.BringAboveNotifications();
            }));
        }

        private void ShowDetectionCompletedWithThreatNotification(string resultPath, int confidencePercent, System.Windows.Media.Imaging.BitmapSource evidenceImage, bool isAudio = false)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DetectionNotificationWindow.CloseAllOpen();
                var popup = new DetectionNotificationWindow();
                popup.SetDetectionCompletedWithThreatContent(confidencePercent, resultPath,
                    openResultsFolder: () =>
                    {
                        try
                        {
                            if (controller != null) controller.OpenResultsFolder();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to open results folder: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    },
                    evidenceImageLeft: evidenceImage,
                    evidenceImageRight: null,
                    navigateToResultsPage: () => NavigateToResultsAndShowSessionDetail(resultPath),
                    isAudio: isAudio);
                popup.ShowAtBottomRight(autoCloseSeconds: 0);
                _floatingWidget?.BringAboveNotifications();
            }));
        }

        // Window Controls
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                e.Handled = true; // Mark event as handled
                this.WindowState = WindowState.Minimized;
                System.Diagnostics.Debug.WriteLine("Minimize button clicked - window state set to Minimized");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error minimizing window: {ex.Message}");
                MessageBox.Show($"Error minimizing window: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (isMaximized)
            {
                WindowState = WindowState.Normal;
                isMaximized = false;
            }
            else
            {
                WindowState = WindowState.Maximized;
                isMaximized = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Theme toggle removed - now handled in Settings screen

        protected override void OnClosed(EventArgs e)
        {
            _floatingWidget?.Close();
            _floatingWidget = null;
            if (notifyIcon != null)
            {
                notifyIcon.Dispose();
            }
            
            if (controller != null)
            {
                try
                {
                    controller.StopVideoDetection();
                }
                catch { }
                controller.Dispose();
            }


            if (statusTimer != null)
            {
                statusTimer.Stop();
            }

            base.OnClosed(e);
            
            // Shell: Single window - shutdown when Shell closes
            Application.Current.Shutdown();
        }
    }

    /// <summary>Color table for tray context menu to match notification UI (white, light blue hover, dark text).</summary>
    internal class TrayMenuColorTable : Forms.ProfessionalColorTable
    {
        private static readonly System.Drawing.Color _notificationHeader = System.Drawing.Color.FromArgb(0xE8, 0xF4, 0xFC);
        private static readonly System.Drawing.Color _menuBorder = System.Drawing.Color.FromArgb(0xD0, 0xD0, 0xD0);
        private static readonly System.Drawing.Color _pressed = System.Drawing.Color.FromArgb(0xD0, 0xE8, 0xF4);
        private static readonly System.Drawing.Color _imageMarginEnd = System.Drawing.Color.FromArgb(0xEE, 0xEE, 0xEE);

        public override System.Drawing.Color MenuBorder => _menuBorder;
        public override System.Drawing.Color MenuItemSelected => _notificationHeader;
        public override System.Drawing.Color MenuItemSelectedGradientBegin => _notificationHeader;
        public override System.Drawing.Color MenuItemSelectedGradientEnd => _notificationHeader;
        public override System.Drawing.Color MenuItemPressedGradientBegin => _pressed;
        public override System.Drawing.Color MenuItemPressedGradientEnd => _pressed;
        public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.White;
        public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.White;
        public override System.Drawing.Color ImageMarginGradientEnd => _imageMarginEnd;
    }

    /// <summary>Renderer for tray context menu using notification-style colors.</summary>
    internal class TrayMenuNotificationStyleRenderer : Forms.ToolStripProfessionalRenderer
    {
        public TrayMenuNotificationStyleRenderer() : base(new TrayMenuColorTable()) { }
    }
}
