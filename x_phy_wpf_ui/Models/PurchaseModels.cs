#nullable enable
using System;
using Newtonsoft.Json;

namespace x_phy_wpf_ui.Models
{
    public class PurchaseRequest
    {
        [JsonProperty("planId")]
        public int PlanId { get; set; }
        public string? PaymentMethodId { get; set; }
    }

    public class PurchaseResponse
    {
        public string PaymentIntentId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public bool RequiresAction { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PurchaseConfirmRequest
    {
        public string PaymentIntentId { get; set; } = string.Empty;
    }

    public class PurchaseConfirmResponse
    {
        public Guid LicenseId { get; set; }
        public string Message { get; set; } = string.Empty;
        /// <summary>Updated license after purchase (Status=Active, PurchaseDate, ExpiryDate, PlanId). Backend should return this so the client can update the UI without re-login.</summary>
        public LicenseInfo? License { get; set; }
        /// <summary>Updated user info if backend includes it.</summary>
        public UserInfo? User { get; set; }
    }

    /// <summary>Request for license validate API (device fingerprint).</summary>
    public class ValidateRequest
    {
        [JsonProperty("deviceFingerprint")]
        public string DeviceFingerprint { get; set; } = string.Empty;
        [JsonProperty("deviceName")]
        public string? DeviceName { get; set; }
    }

    /// <summary>Response from license validate API. Used to refresh license info including trial attempts.</summary>
    public class ValidateResponse
    {
        [JsonProperty("valid")]
        public bool Valid { get; set; }
        [JsonProperty("license")]
        public LicenseInfo? License { get; set; }
        [JsonProperty("errorMessage")]
        public string? ErrorMessage { get; set; }
    }
}
