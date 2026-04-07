using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class LicenseSubscriptionComponent : UserControl
    {
        public event EventHandler? BackRequested;
        public event EventHandler? UpgradePlanRequested;

        private readonly LicenseSubscriptionService _licenseService = new LicenseSubscriptionService();

        public LicenseSubscriptionComponent()
        {
            InitializeComponent();
            Loaded += LicenseSubscriptionComponent_Loaded;
        }

        private async void LicenseSubscriptionComponent_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadLicensesAsync();
        }

        public async System.Threading.Tasks.Task LoadLicensesAsync()
        {
            LoadingText.Visibility = Visibility.Visible;
            PlansList.Visibility = Visibility.Collapsed;
            NoPlansText.Visibility = Visibility.Collapsed;

            var licenses = await _licenseService.GetMyLicensesAsync();
            LoadingText.Visibility = Visibility.Collapsed;

            if (licenses == null || licenses.Count == 0)
            {
                NoPlansText.Visibility = Visibility.Visible;
                return;
            }

            var items = licenses.Select(l => new LicensePlanCardItem
            {
                LicenseId = l.LicenseId,
                PlanDisplayName = string.IsNullOrEmpty(l.PlanName) ? "Plan" : l.PlanName.TrimEnd() + " Plan",
                PlanType = l.PlanType,
                BillingCycle = l.BillingCycle,
                ExpiryDateDisplay = l.ExpiryDateDisplay,
                RenewalAmount = l.RenewalAmount ?? "",
                Status = l.IsActive ? "Active" : "Expired",
                StatusBrush = l.IsActive ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)) : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                RenewalAmountVisibility = string.IsNullOrEmpty(l.RenewalAmount) ? Visibility.Collapsed : Visibility.Visible,
                UpgradeButtonVisibility = l.IsActive ? Visibility.Visible : Visibility.Collapsed,
                UpgradeButtonEnabled = l.IsActive && IsTrialPlan(l)
            }).ToList();

            PlansList.ItemsSource = items;
            PlansList.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void UpgradePlan_Click(object sender, RoutedEventArgs e)
        {
            UpgradePlanRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>True when this license is a trial (enable Upgrade Plan so user can upgrade to paid).</summary>
        private static bool IsTrialPlan(UserLicenseItem l)
        {
            if (string.Equals(l.Status, "Trial", StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(l.BillingCycle) && l.BillingCycle.Trim().ToLowerInvariant().Contains("trial")) return true;
            if (!string.IsNullOrEmpty(l.PlanName) && l.PlanName.Trim().ToLowerInvariant().Contains("trial")) return true;
            if (!string.IsNullOrEmpty(l.PlanType) && l.PlanType.Trim().ToLowerInvariant().Contains("trial")) return true;
            return false;
        }

        private sealed class LicensePlanCardItem
        {
            public Guid LicenseId { get; set; }
            public string PlanDisplayName { get; set; } = "";
            public string PlanType { get; set; } = "";
            public string BillingCycle { get; set; } = "";
            public string ExpiryDateDisplay { get; set; } = "";
            public string RenewalAmount { get; set; } = "";
            public string Status { get; set; } = "";
            public Brush StatusBrush { get; set; } = Brushes.Transparent;
            public Visibility RenewalAmountVisibility { get; set; }
            public Visibility UpgradeButtonVisibility { get; set; }
            /// <summary>True only when this license is Active Trial (Upgrade Plan button enabled only then).</summary>
            public bool UpgradeButtonEnabled { get; set; }
        }
    }
}
