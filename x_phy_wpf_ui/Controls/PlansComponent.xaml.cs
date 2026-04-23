using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class PlansComponent : UserControl
    {
        /// <summary>Where the Plans screen should return when Back is pressed.</summary>
        public enum PlansBackNavigation
        {
            DetectionHome,
            Profile,
            Settings,
            Results
        }

        public event EventHandler<PlanSelectedEventArgs> PlanSelected;
        public event EventHandler BackRequested;

        /// <summary>Last navigation target set via <see cref="SetBackNavigation"/> (e.g. after returning from Stripe).</summary>
        public PlansBackNavigation CurrentBackTarget { get; private set; } = PlansBackNavigation.DetectionHome;

        private readonly LicensePlanService _planService;
        private List<LicensePlanDto> _plans = new();

        public PlansComponent()
        {
            InitializeComponent();
            _planService = new LicensePlanService();
            Loaded += PlansComponent_Loaded;
            IsVisibleChanged += PlansComponent_IsVisibleChanged;
        }

        public void SetBackNavigation(PlansBackNavigation target)
        {
            CurrentBackTarget = target;
            PlansBackButton.Content = target switch
            {
                PlansBackNavigation.Profile => "← Back to Profile",
                PlansBackNavigation.Settings => "← Back to Settings",
                PlansBackNavigation.Results => "← Back to Results",
                _ => "← Back to Detection"
            };
        }

        private async void PlansComponent_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadPlans();
        }

        /// <summary>Reload plans when component becomes visible so we use current license (disable Stripe / highlight current plan).</summary>
        private async void PlansComponent_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible && _plans != null && _plans.Count > 0)
            {
                await LoadPlans();
            }
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
                    StatusText.Foreground = Brushes.Orange;
                    return;
                }

                // Current user license: disable Stripe and highlight current plan when user has an active paid plan (not Trial)
                var tokens = new TokenStorage().GetTokens();
                var licenseInfo = tokens?.LicenseInfo;
                var userInfo = tokens?.UserInfo;
                // Use same status resolution as MainWindow (LicenseInfo.Status else UserInfo.LicenseStatus else Trial)
                string status = licenseInfo?.Status ?? (!string.IsNullOrEmpty(userInfo?.LicenseStatus) ? userInfo.LicenseStatus : "Trial");
                bool hasActivePaidPlan = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(licenseInfo?.PlanName?.Trim(), "Trial", StringComparison.OrdinalIgnoreCase);
                int? currentPlanId = licenseInfo?.PlanId;
                string currentPlanName = licenseInfo?.PlanName?.Trim() ?? "";

                // Convert to view models
                var planViewModels = _plans.Select(p => new PlanViewModel(p, currentPlanId, currentPlanName, hasActivePaidPlan)).ToList();

                PlansItemsControl.ItemsSource = planViewModels;
                PlansScrollViewer.Visibility = Visibility.Visible;
                StatusText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading plans: {ex.Message}";
                StatusText.Foreground = Brushes.Red;
            }
        }

        private void PlanCard_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border)
            {
                var planViewModel = border.DataContext as PlanViewModel ?? border.Tag as PlanViewModel;
                if (planViewModel != null)
                {
                    // Match screenshot: gradient background + thin light blue/cyan border
                    border.Background = planViewModel.CardGradient;
                    border.BorderThickness = new Thickness(1);
                    border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#70C8E0")); // thin light blue/cyan border
                    if (planViewModel.IsCurrentPlan)
                    {
                        border.BorderBrush = (SolidColorBrush)FindResource("PrimaryColor");
                        border.BorderThickness = new Thickness(2);
                    }
                }
            }
        }

        private void StripeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PlanViewModel planViewModel)
            {
                // Map API plan to LicensePlan model (use EffectivePlanId in case API returns "planId" instead of "id")
                var plan = new LicensePlan
                {
                    PlanId = planViewModel.Plan.EffectivePlanId,
                    Name = planViewModel.Plan.Name?.Trim() ?? planViewModel.DisplayName,
                    Price = planViewModel.Plan.Price,
                    DurationDays = planViewModel.DurationDays,
                    Description = string.Join(", ", planViewModel.Features)
                };

                // Raise event to notify parent (MainWindow)
                PlanSelected?.Invoke(this, new PlanSelectedEventArgs(plan));
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        // ViewModel for plan display
        private class PlanViewModel
        {
            private const string MoneyBag = "\uD83D\uDCB0"; // 💰
            private const string Star = "\u2B50";           // ⭐
            private const string Trophy = "\uD83C\uDFC6";  // 🏆

            public LicensePlanDto Plan { get; }
            public string DisplayName { get; }
            /// <summary>Numeric part only (e.g. "29"); "USD" is styled separately in XAML.</summary>
            public string PriceAmount { get; }
            public string PriceSuffix { get; }
            public int DurationDays { get; }
            public List<string> Features { get; }
            public Brush CardGradient { get; }
            public int CardIndex { get; }
            /// <summary>True when user has an active paid plan and this plan is the one they are on.</summary>
            public bool IsCurrentPlan { get; }
            /// <summary>False when user has an active paid plan (Stripe button disabled).</summary>
            public bool IsStripeButtonEnabled { get; }

            private static int _cardIndexCounter = 0;

            private enum PlanUiTier
            {
                Monthly,
                Quarterly,
                Annual,
                SixMonth,
                Other
            }

            public PlanViewModel(LicensePlanDto plan, int? currentPlanId, string currentPlanName, bool hasActivePaidPlan)
            {
                Plan = plan;
                bool matchesById = currentPlanId.HasValue && plan.EffectivePlanId == currentPlanId.Value;
                bool matchesByName = !string.IsNullOrEmpty(currentPlanName) && PlanNamesMatch(plan.Name, currentPlanName);
                IsCurrentPlan = hasActivePaidPlan && (matchesById || matchesByName);
                IsStripeButtonEnabled = !hasActivePaidPlan;
                CardIndex = _cardIndexCounter++;

                var tier = ClassifyPlanTier(plan.Name);
                var baseFeatures = new List<string>
                {
                    "Live Conferencing Video & Audio Detection",
                    "Live Web Video & Audio Detection",
                    "Instant Real-time Alerts",
                    "Evidence & History Logs"
                };

                switch (tier)
                {
                    case PlanUiTier.Monthly:
                        // Blank second line so title block height matches Quarterly/Annual (tag line rows).
                        DisplayName = "MONTHLY" + Environment.NewLine + "\u00A0";
                        PriceAmount = "29";
                        PriceSuffix = " / device / month";
                        DurationDays = 30;
                        Features = new List<string>(baseFeatures);
                        CardGradient = CreateGradientBrush("#8B2D8B", "#4A1A4A");
                        break;
                    case PlanUiTier.Quarterly:
                        DisplayName = "QUARTERLY" + Environment.NewLine + $"{Star} Popular";
                        PriceAmount = "39";
                        PriceSuffix = " / device / 3 months";
                        DurationDays = 90;
                        Features = new List<string>(baseFeatures) { $"{MoneyBag} Save USD 192 per year" };
                        CardGradient = CreateGradientBrush("#6B3A8B", "#3A1A4A");
                        break;
                    case PlanUiTier.Annual:
                        DisplayName = "ANNUALLY" + Environment.NewLine + $"{Trophy} Best Value";
                        PriceAmount = "99";
                        PriceSuffix = " / device / year";
                        DurationDays = 365;
                        Features = new List<string>(baseFeatures) { $"{MoneyBag} Save USD 249 per year" };
                        CardGradient = CreateGradientBrush("#2A5A8B", "#1A2A4A");
                        break;
                    case PlanUiTier.SixMonth:
                        DisplayName = "6-MONTHS";
                        PriceAmount = $"{plan.Price:F0}";
                        PriceSuffix = " / device / 6 months";
                        DurationDays = 180;
                        Features = new List<string>(baseFeatures);
                        CardGradient = CreateGradientBrush("#5A3A8B", "#2A1A4A");
                        break;
                    default:
                        DisplayName = string.IsNullOrWhiteSpace(plan.Name) ? "PLAN" : plan.Name.Trim().ToUpperInvariant();
                        PriceAmount = $"{plan.Price:F0}";
                        PriceSuffix = " / per device";
                        DurationDays = InferDurationDaysFromName(plan.Name);
                        Features = new List<string>(baseFeatures);
                        var gradients = new[]
                        {
                            CreateGradientBrush("#8B2D8B", "#4A1A4A"),
                            CreateGradientBrush("#5A3A8B", "#2A1A4A"),
                            CreateGradientBrush("#2A5A8B", "#1A2A4A")
                        };
                        CardGradient = gradients[CardIndex % 3];
                        break;
                }
            }

            private static PlanUiTier ClassifyPlanTier(string? planName)
            {
                var name = (planName ?? "").Trim().ToLowerInvariant();
                var nameNorm = name.Replace(" ", "").Replace("-", "");
                if (nameNorm.StartsWith("12") || name.Contains("12month") || name.Contains("12 month") || name.Contains("year") || name.Contains("annual"))
                    return PlanUiTier.Annual;
                if (nameNorm.StartsWith("3") || name.Contains("3month") || name.Contains("3 month") || name.Contains("three"))
                    return PlanUiTier.Quarterly;
                if (nameNorm.StartsWith("6") || name.Contains("6month") || name.Contains("6 month") || name.Contains("six") || name.Contains("semi"))
                    return PlanUiTier.SixMonth;
                if (nameNorm == "1month" || nameNorm == "1months" || (nameNorm.StartsWith("1") && !nameNorm.StartsWith("10") && !nameNorm.StartsWith("12") && name.Contains("month")))
                    return PlanUiTier.Monthly;
                return PlanUiTier.Other;
            }

            private static int InferDurationDaysFromName(string? planName)
            {
                var name = (planName ?? "").Trim().ToLowerInvariant();
                var numberMatch = System.Text.RegularExpressions.Regex.Match(name, @"\d+");
                if (numberMatch.Success && int.TryParse(numberMatch.Value, out int months))
                    return Math.Max(30, months * 30);
                return 30;
            }

            private LinearGradientBrush CreateGradientBrush(string color1, string color2)
            {
                var brush = new LinearGradientBrush();
                brush.StartPoint = new Point(0, 0);
                brush.EndPoint = new Point(1, 1);
                brush.GradientStops.Add(new GradientStop(
                    (Color)ColorConverter.ConvertFromString(color1), 0));
                brush.GradientStops.Add(new GradientStop(
                    (Color)ColorConverter.ConvertFromString(color2), 1));
                return brush;
            }

            private static bool PlanNamesMatch(string planName, string currentPlanName)
            {
                if (string.IsNullOrWhiteSpace(planName) || string.IsNullOrWhiteSpace(currentPlanName)) return false;
                var a = planName.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "");
                var b = currentPlanName.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "");
                return a == b || a.Contains(b) || b.Contains(a);
            }
        }
    }

    public class PlanSelectedEventArgs : EventArgs
    {
        public LicensePlan Plan { get; }

        public PlanSelectedEventArgs(LicensePlan plan)
        {
            Plan = plan;
        }
    }
}
