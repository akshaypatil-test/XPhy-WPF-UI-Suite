using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using XPhyWrapper;
using x_phy_wpf_ui.Services;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Controls;

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
        /// <summary>When true, open results folder after stop completes (e.g. after "Stop & View Results" from notification).</summary>
        private bool openResultsFolderAfterStop = false;
        private bool isAudioDetection = false; // Track if current detection is Audio mode (vs Video mode)
        private LicenseManager licenseManager;
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
        private FloatingWidgetWindow _floatingWidget;
        /// <summary>Set to true to show the floating app launcher when minimized. Disabled for now; re-enable later.</summary>
        private const bool FloatingWidgetEnabled = false;

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
                
                // Enable window dragging
                this.MouseDown += MainWindow_MouseDown;
                
                // Ensure window fits on screen
                this.Loaded += MainWindow_Loaded;
                
                // Handle window state changes
                this.StateChanged += MainWindow_StateChanged;

                // When refresh token expires or is invalid, redirect to sign-in
                AuthenticatedApiClient.SessionExpired += AuthenticatedApiClient_SessionExpired;

                // Logout on close only when Remember Me was unchecked at login
                this.Closing += MainWindow_Closing;
                
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

            var elapsed = (DateTime.UtcNow - _loaderShownAt).TotalSeconds;
            if (elapsed < 3.0)
            {
                var delayMs = (int)((3.0 - elapsed) * 1000);
                await Task.Delay(delayMs);
            }

            // 2. Run native license validation (Keygen) by creating the controller. If invalid (e.g. machine limit), show error and do not complete login.
            Dispatcher.Invoke(() =>
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
                    string message = GetControllerInitFailureMessage(ex);
                    bool isLicenseError = IsLicenseValidationFailure(ex);
                    string title = isLicenseError ? "License Error" : "Validation Failed";
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                    AuthPanel.SetContent(signInComponentOnError);
                    return;
                }

                // 3. License valid: save tokens and complete login.
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
                    licenseInfo,
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
            WriteLicenseKeyToExeConfig(licenseKey.Trim());
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
            StatisticsCardsControl.TotalAnalysisTime = "0h";
            StatisticsCardsControl.LastDetection = "Never";
            UpdateLicenseDisplay();
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
        }

        private void ShowAuthView()
        {
            // After logout: go directly to Sign In (skip Welcome and Get Started)
            _signInComponent.ClearInputs();
            AuthPanel.SetContent(_signInComponent);
            AuthPanel.Visibility = Visibility.Visible;
            AppPanel.Visibility = Visibility.Collapsed;
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

            // Signed in from LaunchWindow (tokens saved, no validation there): show app view; controller will init in ShowAppView
            var tokenStorageForLaunch = new TokenStorage();
            var tokensForLaunch = tokenStorageForLaunch.GetTokens();
            if (tokensForLaunch?.UserInfo != null)
            {
                ShowAppView();
                return;
            }

            // Stats and controller init happen when App view is shown (after login), not on initial load when Auth is shown
            if (AppPanel.Visibility == Visibility.Visible)
            {
                StatisticsCardsControl.TotalDetections = "0";
                StatisticsCardsControl.TotalDeepfakes = "0";
                StatisticsCardsControl.TotalAnalysisTime = "0h";
                StatisticsCardsControl.LastDetection = "Never";
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
                
                // Show error (license) or warning (other); match old app title for license errors
                string userMessage = GetControllerInitFailureMessage(ex);
                bool isLicenseError = IsLicenseValidationFailure(ex);
                string title = isLicenseError ? "License Error" : "Controller Initialization Warning";
                var icon = isLicenseError ? MessageBoxImage.Error : MessageBoxImage.Warning;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(userMessage, title, MessageBoxButton.OK, icon);
                }), DispatcherPriority.Normal);
                
                // Set controller to null so we can check for it later
                controller = null;
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
            if (!FloatingWidgetEnabled) return;
            if (WindowState == WindowState.Minimized)
            {
                if (_floatingWidget == null)
                {
                    _floatingWidget = new FloatingWidgetWindow();
                    _floatingWidget.SetOwnerWindow(this);
                }
                _floatingWidget.PositionAtBottomRight();
                _floatingWidget.SetDetectionState(controller != null && controller.IsDetectionRunning(), overallClassification == true);
                _floatingWidget.Show();
            }
            else
            {
                _floatingWidget?.Hide();
            }
        }


        private async void StartDetectionFromPopup(DetectionSource source, bool isLiveCallMode, bool isAudioMode, bool isRetry = false)
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
                    MessageBox.Show("Detection is already running.", "Information", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
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
                });

                // Track detection mode
                isWebSurfingMode = !isLiveCallMode;
                isAudioDetection = isAudioMode;
                isStoppingDetection = false;

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

                // Start detection based on mode (Video or Audio)
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
                                                {
                                                    openResultsFolderAfterStop = false;
                                                    try { controller?.OpenResultsFolder(); } catch { }
                                                }
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
                                                {
                                                    openResultsFolderAfterStop = false;
                                                    try { controller?.OpenResultsFolder(); } catch { }
                                                }
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
                                            {
                                                openResultsFolderAfterStop = false;
                                                try { controller?.OpenResultsFolder(); } catch { }
                                            }
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
                                            {
                                                openResultsFolderAfterStop = false;
                                                try { controller?.OpenResultsFolder(); } catch { }
                                            }
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
            // Update floating widget ring (rotate when detecting, red when deepfake) — only when enabled
            if (FloatingWidgetEnabled && _floatingWidget != null && _floatingWidget.IsVisible)
            {
                bool isRunning = controller != null && controller.IsDetectionRunning();
                _floatingWidget.SetDetectionState(isRunning, overallClassification == true);
            }
        }

        private void DetectionResultsComponent_DeepfakeDetected(object sender, EventArgs e)
        {
            ShowDeepfakeNotification();
        }

        private void TopNavBar_NavigationClicked(object sender, string page)
        {
            switch (page)
            {
                case "Home":
                    ShowDetectionContent();
                    ResetAppContentToHome();
                    break;
                case "Results":
                    MessageBox.Show("Results page coming soon!", "Results", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case "Profile":
                    // TODO: Show profile page
                    MessageBox.Show("Profile page coming soon!", "Profile", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case "Settings":
                    // TODO: Show settings page
                    MessageBox.Show("Settings page coming soon!", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }
        
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to log out?", "Logout", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Clear stored tokens (standard auth: clean logout for multiple login/logout cycles)
                    var tokenStorage = new TokenStorage();
                    tokenStorage.ClearTokens();

                    // Release native controller so the next user gets a fresh init with their license.
                    // This ensures Keygen validates the current user's key against this machine (one user per machine).
                    controller = null;
                    controllerInitializationAttempted = false;

                    // Shell: Show LoginView again; do not close the app
                    ShowAuthView();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error during logout: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void BottomBar_SupportClicked(object sender, EventArgs e)
        {
            MessageBox.Show("For support, please contact us at support@xphy.com", "Support", 
                MessageBoxButton.OK, MessageBoxImage.Information);
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
                    ShowAuthView();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SessionExpired handler: {ex.Message}");
                }
            });
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                var tokenStorage = new TokenStorage();
                var tokens = tokenStorage.GetTokens();
                if (tokens != null && !tokens.RememberMe)
                {
                    tokenStorage.ClearTokens();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow_Closing: {ex.Message}");
            }
        }

        private void BottomBar_SubscribeClicked(object sender, EventArgs e)
        {
            ShowPlansComponent();
        }

        private void ShowPlansComponent()
        {
            // Hide detection content and statistics (hide entire Grid row)
            DetectionContentGrid.Visibility = Visibility.Collapsed;
            StatisticsCardsGrid.Visibility = Visibility.Collapsed;
            
            // Show plans component
            PlansComponent.Visibility = Visibility.Visible;
            StripePaymentComponentContainer.Visibility = Visibility.Collapsed;
        }

        private void ShowDetectionContent()
        {
            // Show detection content and statistics
            DetectionContentGrid.Visibility = Visibility.Visible;
            StatisticsCardsGrid.Visibility = Visibility.Visible;
            
            // Hide plans, payment, and corp register components
            PlansComponent.Visibility = Visibility.Collapsed;
            StripePaymentComponentContainer.Visibility = Visibility.Collapsed;
            CorpRegisterComponent.Visibility = Visibility.Collapsed;
        }

        private void PlansComponent_PlanSelected(object sender, PlanSelectedEventArgs e)
        {
            // Hide plans component and statistics
            PlansComponent.Visibility = Visibility.Collapsed;
            StatisticsCardsGrid.Visibility = Visibility.Collapsed;
            
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
            var successWindow = new PaymentSuccessWindow(
                e.PlanName,
                e.DurationDays,
                e.Price,
                e.PaymentIntentId
            );
            successWindow.Show();
            successWindow.Closed += (s, args) =>
            {
                UpdateLicenseDisplay();
                ShowDetectionContent();
            };
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
                    MessageBox.Show("Detection is already running.", "Information", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Consume one trial detection attempt (Trial users: decrements count; paid: always allowed)
                var licenseService = new LicensePurchaseService();
                var attemptResult = await licenseService.UseDetectionAttemptAsync();
                if (!attemptResult.Allowed)
                {
                    MessageBox.Show(
                        attemptResult.Message ?? "You have no detection attempts remaining. Please subscribe to continue.",
                        "Detection Not Allowed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
                // Update bottom bar with current trial attempts (TrialAttemptsRemaining is null for paid licenses)
                if (attemptResult.TrialAttemptsRemaining.HasValue)
                    BottomBar.Attempts = attemptResult.TrialAttemptsRemaining.Value;

                // Start detection with selected parameters
                StartDetectionFromPopup(e.SelectedSource.Value, e.IsLiveCallMode, e.IsAudioMode);
                
                // Hide detection selection container and show results panel
                DetectionSelectionContainer.Visibility = Visibility.Collapsed;
                DetectionResultsPanel.Visibility = Visibility.Visible;

                // Minimize MainWindow and bring the selected process window to the foreground
                this.WindowState = WindowState.Minimized;
                ProcessDetectionService.BringProcessWindowToForeground(e.SelectedProcess.ProcessId);
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
                                openResultsFolderAfterStop = false;
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
                    openResultsFolderAfterStop = false;
                    try { controller?.OpenResultsFolder(); } catch { }
                }
                ResetAppContentToHome();
                RefreshLicenseDisplayAfterDetectionAsync();
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
                    var tokenStorage = new TokenStorage();
                    var tokens = tokenStorage.GetTokens();
                    if (tokens != null)
                        tokenStorage.UpdateUserAndLicense(tokens.UserInfo, result.License);
                    UpdateLicenseDisplay();
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
        }

        private void UpdateAudioClassification(int classification)
        {
            switch (classification)
            {
                case 0: overallClassification = false; break;
                case 1: overallClassification = true; _deepfakeDetectedDuringRun = true; break;
                default: overallClassification = null; break;
            }
            DetectionResultsComponent?.UpdateAudioClassification(classification);
        }

        private void ShowFinalResult(string resultPath)
        {
            DetectionResultsPanel.Visibility = Visibility.Visible;
            string displayPath = resultPath;
            if (string.IsNullOrEmpty(displayPath))
            {
                try { displayPath = controller?.GetResultsDir() ?? ""; } catch { }
            }
            DetectionResultsComponent?.ShowFinalResult(displayPath ?? resultPath);

            int faceCount = DetectionResultsComponent?.DetectedFacesCount ?? 0;
            bool hadDeepfake = _deepfakeDetectedDuringRun;
            if (hadDeepfake)
            {
                int confidence = DetectionResultsComponent?.LastConfidencePercent ?? 97;
                var evidenceImage = DetectionResultsComponent?.LatestEvidenceImage;
                ShowDetectionCompletedWithThreatNotification(displayPath ?? "", confidence, evidenceImage);
            }
            else
            {
                ShowDetectionCompletedNotification("No AI Manipulation Found", displayPath ?? "");
            }

            if (isStoppingDetection)
            {
                isStoppingDetection = false;
                System.Diagnostics.Debug.WriteLine("ShowFinalResult: Final result shown, stopping flag cleared");
            }
        }

        // Notification helper
        private Forms.NotifyIcon notifyIcon;
        
        private void InitializeLicenseManager()
        {
            licenseManager = new LicenseManager();
            UpdateLicenseDisplay();
        }

        /// <summary>Derives license duration in days from plan name (e.g. "1 Month" -> 30, "12 Months" -> 365).</summary>
        private static int GetDurationDaysFromPlanName(string planName)
        {
            if (string.IsNullOrWhiteSpace(planName)) return 30;
            var name = planName.ToLowerInvariant();
            if (name.StartsWith("3") || name.Contains("3month") || name.Contains("three")) return 90;
            if (name.StartsWith("6") || name.Contains("6month") || name.Contains("six") || name.Contains("semi")) return 180;
            if (name.StartsWith("12") || name.Contains("12month") || name.Contains("year") || name.Contains("annual")) return 365;
            var m = Regex.Match(name, @"\d+");
            if (m.Success && int.TryParse(m.Value, out int months)) return months * 30;
            return 30; // 1 month default
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
                    // When LicenseInfo is null (e.g. old session), treat as Trial if User has TrialEndsAt
                    string status = licenseInfo?.Status ?? (!string.IsNullOrEmpty(userInfo.LicenseStatus) ? userInfo.LicenseStatus : "Trial");
                    int daysRemaining = 0;
                    
                    // Calculate remaining days based on license status
                    if (status.Equals("Trial", StringComparison.OrdinalIgnoreCase))
                    {
                        if (userInfo.TrialEndsAt.HasValue)
                            daysRemaining = Math.Max(0, (int)Math.Ceiling((userInfo.TrialEndsAt.Value - DateTime.UtcNow).TotalDays));
                        else if (licenseInfo?.TrialEndsAt.HasValue == true)
                            daysRemaining = Math.Max(0, (int)Math.Ceiling((licenseInfo.TrialEndsAt.Value - DateTime.UtcNow).TotalDays));
                        BottomBar.Status = "Trial";
                        BottomBar.RemainingDays = daysRemaining;
                        BottomBar.Attempts = licenseInfo?.TrialAttemptsRemaining;
                        BottomBar.ShowSubscribeButton = true;
                    }
                    else if (licenseInfo != null && status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    {
                        // For Active: Prefer ExpiryDate from License table; else compute from PurchaseDate + plan duration
                        if (licenseInfo.ExpiryDate.HasValue)
                        {
                            daysRemaining = Math.Max(0, (int)Math.Ceiling((licenseInfo.ExpiryDate.Value - DateTime.UtcNow).TotalDays));
                        }
                        else if (licenseInfo.PurchaseDate.HasValue)
                        {
                            // Backend sends PurchaseDate only (no ExpiryDate); derive duration from plan name
                            int durationDays = 365; // Default 1 year
                            if (!string.IsNullOrWhiteSpace(licenseInfo.PlanName))
                            {
                                durationDays = GetDurationDaysFromPlanName(licenseInfo.PlanName);
                            }
                            else if (licenseInfo.PlanId.HasValue)
                            {
                                try
                                {
                                    var planService = new LicensePlanService();
                                    var plans = await planService.GetPlansAsync();
                                    var plan = plans.FirstOrDefault(p => p.EffectivePlanId == licenseInfo.PlanId.Value);
                                    if (plan != null)
                                        durationDays = GetDurationDaysFromPlanName(plan.Name);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error fetching plan for duration: {ex.Message}");
                                }
                            }
                            var expiryDate = licenseInfo.PurchaseDate.Value.AddDays(durationDays);
                            daysRemaining = Math.Max(0, (int)Math.Ceiling((expiryDate - DateTime.UtcNow).TotalDays));
                        }
                        else
                        {
                            daysRemaining = 0;
                        }
                        
                        BottomBar.Status = "Active";
                        BottomBar.RemainingDays = daysRemaining;
                        BottomBar.Attempts = null; // Paid license: no trial attempts display
                        BottomBar.ShowSubscribeButton = false;
                    }
                    else
                    {
                        BottomBar.Status = "Expired";
                        BottomBar.RemainingDays = 0;
                        BottomBar.Attempts = null;
                        BottomBar.ShowSubscribeButton = true;
                    }
                }
                else
                {
                    BottomBar.Status = "No License";
                    BottomBar.RemainingDays = 0;
                    BottomBar.Attempts = null;
                    BottomBar.ShowSubscribeButton = true;
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
                }
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

        private void SubscribeNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Hide this window before opening plans window
                this.Hide();
                
                var plansWindow = new PlansWindow();
                plansWindow.Show();
                
                // Handle plans window closed event
                plansWindow.Closed += (s, args) =>
                {
                    // Show this window again when plans window closes
                    this.Show();
                    UpdateLicenseDisplay();
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open plans: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                // Show this window again if there was an error
                this.Show();
            }
        }
        
        private void InitializeNotifications()
        {
            notifyIcon = new Forms.NotifyIcon();
            notifyIcon.Icon = System.Drawing.SystemIcons.Information;
            notifyIcon.Visible = true;
            notifyIcon.BalloonTipClicked += (s, e) => { notifyIcon.Visible = false; };
        }
        
        private void ShowNotification(string title, string message, Forms.ToolTipIcon icon)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var popup = new DetectionNotificationWindow();
                popup.SetContent(title ?? "", message ?? "");
                popup.ShowAtBottomRight(autoCloseSeconds: 5);
            }));
        }

        private void ShowDeepfakeNotification()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string resultPath = "";
                try { resultPath = controller?.GetResultsDir() ?? ""; } catch { }
                int confidence = DetectionResultsComponent?.LastConfidencePercent ?? 0;
                if (confidence <= 0) confidence = 97;
                var evidenceImage = DetectionResultsComponent?.LatestEvidenceImage;

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
                        openResultsFolderAfterStop = true;
                        StopDetection_Click(null, EventArgs.Empty);
                    },
                    evidenceImageLeft: evidenceImage,
                    evidenceImageRight: null);
                popup.ShowAtBottomRight(autoCloseSeconds: 4);
            }));
        }

        private void ShowDetectionCompletedNotification(string message, string resultPath)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var popup = new DetectionNotificationWindow();
                popup.SetDetectionCompletedContent(message, resultPath, () =>
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
                });
                popup.ShowAtBottomRight(autoCloseSeconds: 0);
            }));
        }

        private void ShowDetectionCompletedWithThreatNotification(string resultPath, int confidencePercent, System.Windows.Media.Imaging.BitmapSource evidenceImage)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var popup = new DetectionNotificationWindow();
                popup.SetDetectionCompletedWithThreatContent(confidencePercent, resultPath, () =>
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
                }, evidenceImage, null);
                popup.ShowAtBottomRight(autoCloseSeconds: 0);
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
}
