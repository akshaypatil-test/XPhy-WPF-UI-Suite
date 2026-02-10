using System;

namespace x_phy_wpf_ui.Models
{
    public class LicensePlan
    {
        public int PlanId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int DurationDays { get; set; }
        public string Description { get; set; }
        public bool IsPopular { get; set; }

        public string DurationText
        {
            get
            {
                if (DurationDays >= 365)
                    return $"{DurationDays / 365} Year{(DurationDays / 365 > 1 ? "s" : "")}";
                if (DurationDays >= 30)
                    return $"{DurationDays / 30} Month{(DurationDays / 30 > 1 ? "s" : "")}";
                return $"{DurationDays} Days";
            }
        }
    }

    public class UserLicense
    {
        public bool IsActive { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string PlanName { get; set; }
        public decimal AmountPaid { get; set; }
        public string TransactionId { get; set; }

        public int DaysRemaining
        {
            get
            {
                var remaining = (ExpiryDate - DateTime.Now).Days;
                return remaining > 0 ? remaining : 0;
            }
        }

        public bool IsExpired => DateTime.Now > ExpiryDate;
    }
}
