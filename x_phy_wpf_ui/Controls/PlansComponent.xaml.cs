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
        public event EventHandler<PlanSelectedEventArgs> PlanSelected;
        public event EventHandler BackRequested;

        private readonly LicensePlanService _planService;
        private List<LicensePlanDto> _plans = new();

        public PlansComponent()
        {
            InitializeComponent();
            _planService = new LicensePlanService();
            Loaded += PlansComponent_Loaded;
        }

        private async void PlansComponent_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadPlans();
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

                // Convert to view models
                var planViewModels = _plans.Select(p => new PlanViewModel(p)).ToList();

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
                    border.Background = planViewModel.CardGradient;
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
                    Name = planViewModel.DisplayName,
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
            public LicensePlanDto Plan { get; }
            public string DisplayName { get; }
            public string PriceText { get; }
            public string PriceSuffix { get; }
            public int DurationDays { get; }
            public List<string> Features { get; }
            public Brush CardGradient { get; }
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
