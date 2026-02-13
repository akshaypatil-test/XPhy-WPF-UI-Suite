using System;

namespace x_phy_wpf_ui.Models
{
    public class UserLicenseItem
    {
        public Guid LicenseId { get; set; }
        public string PlanName { get; set; } = "";
        public string PlanType { get; set; } = "";
        public string BillingCycle { get; set; } = "";
        public decimal Price { get; set; }
        public string Status { get; set; } = ""; // "Active", "Expired"
        public DateTime? PurchasedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public string? RenewalAmount { get; set; }

        public string ExpiryDateDisplay => ExpiryDate.HasValue ? ExpiryDate.Value.ToString("MMM d, yyyy") : "—";
    }
}
