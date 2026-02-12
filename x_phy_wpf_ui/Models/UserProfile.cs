using System;

namespace x_phy_wpf_ui.Models
{
    public class UserProfile
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Username { get; set; } = "";
        public DateTime AccountCreated { get; set; }
        public bool EmailPreferences { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; }
        public string? PlanType { get; set; }
        public DateTime? PlanExpiryDate { get; set; }
        public string? SubscriptionStatus { get; set; }

        public string FullName => $"{FirstName} {LastName}".Trim();
        public string AccountCreatedDisplay => AccountCreated == default ? "—" : AccountCreated.ToString("MMMM d, yyyy");
        public string LastLoginDisplay => LastLoginAt.HasValue ? FormatLastLogin(LastLoginAt.Value) : "—";
        public string PlanExpiryDisplay => PlanExpiryDate.HasValue ? PlanExpiryDate.Value.ToString("MMMM d, yyyy") : "—";
        public string MemberSinceDisplay => AccountCreated == default ? "—" : MemberSinceYears(AccountCreated);
        public string AccountStatusDisplay => IsActive ? "Verified" : "Inactive";

        private static string FormatLastLogin(DateTime dt)
        {
            var now = DateTime.Now;
            if (dt.Date == now.Date) return $"Today, {dt:h:mm tt}";
            if (dt.Date == now.Date.AddDays(-1)) return $"Yesterday, {dt:h:mm tt}";
            return dt.ToString("MMMM d, yyyy, h:mm tt");
        }

        private static string MemberSinceYears(DateTime created)
        {
            var years = (DateTime.UtcNow - created).TotalDays / 365.25;
            if (years < 1) return "Less than 1 Year";
            return $"{(int)years} Year{((int)years == 1 ? "" : "s")}";
        }
    }
}
