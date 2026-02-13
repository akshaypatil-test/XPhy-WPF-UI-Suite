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
        public event EventHandler? ViewFullDetailsRequested;
        public event EventHandler? ViewPlansRequested;

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
                SubscriptionStatusTagText.Text = "—";
                PlanTypeValue.Text = "—";
                PlanExpiryValue.Text = "—";
                MemberSinceValue.Text = "—";
                LastLoginValue.Text = "—";
                AccountStatusText.Text = "—";
                return;
            }

            ProfileFullName.Text = profile.FullName;
            FirstNameBox.Text = profile.FirstName;
            LastNameBox.Text = profile.LastName;
            UsernameBox.Text = profile.Username;
            AccountCreatedText.Text = profile.AccountCreatedDisplay;
            MemberSinceValue.Text = profile.MemberSinceDisplay;
            LastLoginValue.Text = profile.LastLoginDisplay;
            AccountStatusText.Text = profile.AccountStatusDisplay;

            PlanNameText.Text = string.IsNullOrEmpty(profile.PlanType) ? "—" : profile.PlanType + " Plan";
            SubscriptionStatusTagText.Text = profile.SubscriptionStatus ?? "—";
            PlanTypeValue.Text = profile.PlanType ?? "—";
            PlanExpiryValue.Text = profile.PlanExpiryDisplay;

            if (!profile.IsActive && AccountStatusTag != null)
            {
                var orangeRed = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.OrangeRed);
                AccountStatusTag.BorderBrush = orangeRed;
                AccountStatusText.Foreground = orangeRed;
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

        private void ViewFullDetails_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            ViewFullDetailsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ViewPlans_Click(object sender, RoutedEventArgs e)
        {
            ViewPlansRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
