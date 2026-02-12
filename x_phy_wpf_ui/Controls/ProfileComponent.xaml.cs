using System;
using System.Windows;
using System.Windows.Controls;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class ProfileComponent : UserControl
    {
        public event EventHandler? BackRequested;
        public event EventHandler? ChangePasswordRequested;

        private readonly UserProfileService _profileService = new UserProfileService();

        public ProfileComponent()
        {
            InitializeComponent();
            Loaded += ProfileComponent_Loaded;
        }

        private async void ProfileComponent_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadProfileAsync();
        }

        public async System.Threading.Tasks.Task LoadProfileAsync()
        {
            var profile = await _profileService.GetProfileAsync();
            if (profile == null)
            {
                ProfileFullName.Text = "—";
                FirstNameBox.Text = "";
                LastNameBox.Text = "";
                UsernameBox.Text = "";
                AccountCreatedText.Text = "—";
                PlanNameText.Text = "—";
                SubscriptionStatusTag.Text = "—";
                PlanTypeText.Text = "Plan Type: —";
                PlanExpiryText.Text = "Expiry Date: —";
                MemberSinceText.Text = "Member Since: —";
                LastLoginText.Text = "Last Login: —";
                AccountStatusText.Text = "—";
                return;
            }

            ProfileFullName.Text = profile.FullName;
            FirstNameBox.Text = profile.FirstName;
            LastNameBox.Text = profile.LastName;
            UsernameBox.Text = profile.Username;
            AccountCreatedText.Text = profile.AccountCreatedDisplay;
            MemberSinceText.Text = "Member Since: " + profile.MemberSinceDisplay;
            LastLoginText.Text = "Last Login: " + profile.LastLoginDisplay;
            AccountStatusText.Text = profile.AccountStatusDisplay;

            PlanNameText.Text = string.IsNullOrEmpty(profile.PlanType) ? "—" : profile.PlanType + " Plan";
            SubscriptionStatusTag.Text = profile.SubscriptionStatus ?? "—";
            PlanTypeText.Text = "Plan Type: " + (profile.PlanType ?? "—");
            PlanExpiryText.Text = "Expiry Date: " + profile.PlanExpiryDisplay;

            if (!profile.IsActive && AccountStatusTag != null)
            {
                AccountStatusTag.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.OrangeRed);
                AccountStatusText.Text = "Inactive";
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            ChangePasswordRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
