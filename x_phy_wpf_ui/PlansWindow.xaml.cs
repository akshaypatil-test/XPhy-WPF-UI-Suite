using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;
using x_phy_wpf_ui.Controls;

namespace x_phy_wpf_ui
{
    public partial class PlansWindow : Window
    {
        private readonly LicensePlanService _planService;
        private List<LicensePlanDto> _plans = new();

        public PlansWindow()
        {
            InitializeComponent();
            _planService = new LicensePlanService();
            Loaded += PlansWindow_Loaded;
            UpdateLicenseStatus();
        }

        private async void PlansWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadPlans();
        }
        
        private async void UpdateLicenseStatus()
        {
            try
            {
                var tokenStorage = new TokenStorage();
                var storedTokens = tokenStorage.GetTokens();
                
                if (BottomBar == null) return;
                
                if (storedTokens?.UserInfo != null)
                {
                    var userInfo = storedTokens.UserInfo;
                    var licenseInfo = storedTokens.LicenseInfo;
                    string status = licenseInfo?.Status ?? (!string.IsNullOrEmpty(userInfo.LicenseStatus) ? userInfo.LicenseStatus : "Trial");
                    int daysRemaining = 0;
                    
                    if (status.Equals("Trial", StringComparison.OrdinalIgnoreCase))
                    {
                        if (userInfo.TrialEndsAt.HasValue)
                            daysRemaining = Math.Max(0, (int)Math.Ceiling((userInfo.TrialEndsAt.Value - DateTime.UtcNow).TotalDays));
                        else if (licenseInfo?.TrialEndsAt.HasValue == true)
                            daysRemaining = Math.Max(0, (int)Math.Ceiling((licenseInfo.TrialEndsAt.Value - DateTime.UtcNow).TotalDays));
                        BottomBar.Status = "Trial";
                        BottomBar.RemainingDays = daysRemaining;
                    }
                    else if (licenseInfo != null && status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    {
                        if (licenseInfo.ExpiryDate.HasValue)
                        {
                            daysRemaining = Math.Max(0, (int)Math.Ceiling((licenseInfo.ExpiryDate.Value - DateTime.UtcNow).TotalDays));
                        }
                        else if (licenseInfo.PurchaseDate.HasValue)
                        {
                            // Backend sends PurchaseDate only (no ExpiryDate); derive duration from plan name
                            int durationDays = 365;
                            if (!string.IsNullOrWhiteSpace(licenseInfo.PlanName))
                            {
                                durationDays = GetDurationDaysFromPlanName(licenseInfo.PlanName);
                            }
                            else if (licenseInfo.PlanId.HasValue)
                            {
                                try
                                {
                                    var plans = await _planService.GetPlansAsync();
                                    var plan = plans?.FirstOrDefault(p => p.EffectivePlanId == licenseInfo.PlanId.Value);
                                    if (plan != null)
                                        durationDays = GetDurationDaysFromPlanName(plan.Name);
                                }
                                catch { }
                            }
                            var expiryDate = licenseInfo.PurchaseDate.Value.AddDays(durationDays);
                            daysRemaining = Math.Max(0, (int)Math.Ceiling((expiryDate - DateTime.UtcNow).TotalDays));
                        }
                        BottomBar.Status = "Active";
                        BottomBar.RemainingDays = daysRemaining;
                    }
                    else
                    {
                        BottomBar.Status = "Expired";
                        BottomBar.RemainingDays = 0;
                    }
                }
                else
                {
                    BottomBar.Status = "Expired";
                    BottomBar.RemainingDays = 0;
                }
            }
            catch
            {
                if (BottomBar != null)
                {
                    BottomBar.Status = "Expired";
                    BottomBar.RemainingDays = 0;
                }
            }
        }

        private static int GetDurationDaysFromPlanName(string planName)
        {
            if (string.IsNullOrWhiteSpace(planName)) return 30;
            var name = planName.ToLowerInvariant();
            if (name.StartsWith("3") || name.Contains("3month") || name.Contains("three")) return 90;
            if (name.StartsWith("6") || name.Contains("6month") || name.Contains("six") || name.Contains("semi")) return 180;
            if (name.StartsWith("12") || name.Contains("12month") || name.Contains("year") || name.Contains("annual")) return 365;
            var m = System.Text.RegularExpressions.Regex.Match(name, @"\d+");
            if (m.Success && int.TryParse(m.Value, out int months)) return months * 30;
            return 30;
        }

        private async Task LoadPlans()
        {
            try
            {
                StatusText.Text = "Loading plans...";
                StatusText.Visibility = Visibility.Visible;
                PlansScrollViewer.Visibility = Visibility.Collapsed;

                _plans = await _planService.GetPlansAsync();

                if (_plans == null || _plans.Count == 0)
                {
                    StatusText.Text = "No plans available at this time.";
                    StatusText.Foreground = System.Windows.Media.Brushes.Orange;
                    return;
                }

                // Convert to view models
                var planViewModels = _plans.Select(p => new PlanViewModel(p)).ToList();

                PlansItemsControl.ItemsSource = planViewModels;
                PlansScrollViewer.Visibility = Visibility.Visible;
                StatusText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading plans: {ex.Message}";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void PlanCard_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border)
            {
                var planViewModel = border.DataContext as PlanViewModel ?? border.Tag as PlanViewModel;
                if (planViewModel != null)
                {
                    border.Background = planViewModel.CardGradient;
                }
            }
        }

        private void StripeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PlanViewModel planViewModel)
            {
                OpenStripePaymentWindow(planViewModel);
            }
        }

        private void OpenStripePaymentWindow(PlanViewModel planViewModel)
        {
            try
            {
                // Map API plan to LicensePlan model (use EffectivePlanId in case API returns "planId" instead of "id")
                var plan = new LicensePlan
                {
                    PlanId = planViewModel.Plan.EffectivePlanId,
                    Name = planViewModel.DisplayName,
                    Price = planViewModel.Plan.Price,
                    DurationDays = planViewModel.DurationDays,
                    Description = string.Join(", ", planViewModel.Features)
                };

                // Hide this window before opening payment window
                this.Hide();
                
                var paymentWindow = new StripePaymentWindow(plan);
                paymentWindow.Show();
                
                // Handle payment window closed event
                paymentWindow.Closed += (s, args) =>
                {
                    // Show this window again when payment window closes; refresh license display (may have changed after purchase)
                    this.Show();
                    UpdateLicenseStatus();
                };
                
                // When payment succeeds, PaymentSuccessWindow is shown; when user closes it, StripePaymentWindow closes and we show again
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open payment window: {ex.Message}", 
                    "Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }

        private void TopNavBar_NavigationClicked(object sender, string page)
        {
            switch (page)
            {
                case "Home":
                    // Find existing MainWindow or create new one
                    MainWindow mainWindow = null;
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is MainWindow mw)
                        {
                            mainWindow = mw;
                            break;
                        }
                    }
                    
                    if (mainWindow == null)
                    {
                        mainWindow = new MainWindow();
                    }
                    
                    mainWindow.Show();
                    mainWindow.Activate();
                    this.Close();
                    break;
                case "Results":
                    // Open results folder
                    try
                    {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string resultsPath = Path.Combine(appData, "X-PHY", "X-PHY Deepfake Detector", "Results");
                        if (Directory.Exists(resultsPath))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", resultsPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to open results folder: {ex.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break;
                case "Profile":
                    MessageBox.Show("Profile page coming soon!", "Profile", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case "Settings":
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
                    // Clear stored tokens
                    var tokenStorage = new TokenStorage();
                    tokenStorage.ClearTokens();
                    
                    // Close the application
                    Application.Current.Shutdown();
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

        private void BottomBar_SubscribeClicked(object sender, EventArgs e)
        {
            // Already on plans page, do nothing
        }
        
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Show MainWindow when closing PlansWindow
            MainWindow mainWindow = null;
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mw)
                {
                    mainWindow = mw;
                    break;
                }
            }
            
            if (mainWindow != null)
            {
                mainWindow.Show();
                mainWindow.Activate();
            }
            
            this.Close();
        }
        
        protected override void OnClosed(EventArgs e)
        {
            // Ensure MainWindow is shown when this window closes (only if no other windows are opening)
            // Check if PaymentSuccessWindow or StripePaymentWindow are still open
            bool hasPaymentWindows = false;
            foreach (Window window in Application.Current.Windows)
            {
                if (window is PaymentSuccessWindow || window is StripePaymentWindow)
                {
                    hasPaymentWindows = true;
                    break;
                }
            }
            
            // Only show MainWindow if no payment windows are active
            if (!hasPaymentWindows)
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow mainWindow && !mainWindow.IsVisible)
                    {
                        mainWindow.Show();
                        break;
                    }
                }
            }
            base.OnClosed(e);
        }

        // ViewModel for plan display
        private class PlanViewModel
        {
            public LicensePlanDto Plan { get; }
            public string DisplayName { get; }
            public string PriceText { get; }
            public string PriceSuffix { get; }
            public int DurationDays { get; }
            public List<string> Features { get; }
            public System.Windows.Media.Brush CardGradient { get; }
            public int CardIndex { get; }

            private static int _cardIndexCounter = 0;

            public PlanViewModel(LicensePlanDto plan)
            {
                Plan = plan;
                CardIndex = _cardIndexCounter++;
                
                // Use the actual plan name from API
                DisplayName = plan.Name;
                
                // Use the actual price from API
                PriceText = $"${plan.Price:F0}";
                PriceSuffix = " / per device";
                
                // Parse plan name to determine duration, features, and gradient
                var name = plan.Name.ToLower();
                if (name.StartsWith("3") || name.Contains("3month") || name.Contains("three"))
                {
                    DurationDays = 90; // 3 months = 90 days
                    Features = new List<string>
                    {
                        "Unlimited detections",
                        "Real-time alerts",
                        "Advanced reporting",
                        "Email support"
                    };
                    CardGradient = CreateGradientBrush("#8B2D8B", "#4A1A4A");
                }
                else if (name.StartsWith("6") || name.Contains("6month") || name.Contains("six") || name.Contains("semi"))
                {
                    DurationDays = 180; // 6 months = 180 days
                    Features = new List<string>
                    {
                        "Everything in 3-Month",
                        "Priority support",
                        "Custom scan schedules",
                        "Save 13%"
                    };
                    CardGradient = CreateGradientBrush("#5A3A8B", "#2A1A4A");
                }
                else if (name.StartsWith("12") || name.Contains("12month") || name.Contains("year") || name.Contains("annual"))
                {
                    DurationDays = 365; // 12 months = 365 days
                    Features = new List<string>
                    {
                        "Everything in 6-Month",
                        "Dedicated account manager",
                        "Advanced analytics",
                        "Save 21%"
                    };
                    CardGradient = CreateGradientBrush("#2A5A8B", "#1A2A4A");
                }
                else
                {
                    // Default fallback - try to extract number from name
                    var numberMatch = System.Text.RegularExpressions.Regex.Match(name, @"\d+");
                    if (numberMatch.Success && int.TryParse(numberMatch.Value, out int months))
                    {
                        DurationDays = months * 30;
                    }
                    else
                    {
                        DurationDays = 30; // Default to 1 month
                    }
                    
                    Features = new List<string>
                    {
                        "Unlimited detections",
                        "Real-time alerts",
                        "Advanced reporting",
                        "Email support"
                    };
                    
                    // Use different gradients based on card index
                    var gradients = new[]
                    {
                        CreateGradientBrush("#8B2D8B", "#4A1A4A"),
                        CreateGradientBrush("#5A3A8B", "#2A1A4A"),
                        CreateGradientBrush("#2A5A8B", "#1A2A4A")
                    };
                    CardGradient = gradients[CardIndex % 3];
                }
            }

            private System.Windows.Media.LinearGradientBrush CreateGradientBrush(string color1, string color2)
            {
                var brush = new System.Windows.Media.LinearGradientBrush();
                brush.StartPoint = new System.Windows.Point(0, 0);
                brush.EndPoint = new System.Windows.Point(1, 1);
                brush.GradientStops.Add(new System.Windows.Media.GradientStop(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color1), 0));
                brush.GradientStops.Add(new System.Windows.Media.GradientStop(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color2), 1));
                return brush;
            }
        }
    }
}
