#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace x_phy_wpf_ui.Models
{
    /// <summary>Matches backend XPhy.Licensing.Api.Models.UserType.</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum UserType
    {
        NonCorp = 0,
        Corp = 1,
        Admin = 2,
    }

    // Request DTOs
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DeviceFingerprint { get; set; } = string.Empty;
        /// <summary>Optional license key for corporate sign-in.</summary>
        [JsonProperty("licenseKey")]
        public string? LicenseKey { get; set; }
    }

    public class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        [JsonProperty("firstName")]
        public string FirstName { get; set; } = string.Empty;
        [JsonProperty("lastName")]
        public string LastName { get; set; } = string.Empty;
        [JsonProperty("userType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public UserType UserType { get; set; } = UserType.NonCorp;
        [JsonProperty("maxDevices")]
        public int? MaxDevices { get; set; }
        [JsonProperty("policyNumber")]
        public string? PolicyNumber { get; set; }
        [JsonProperty("organizationName")]
        public string? OrganizationName { get; set; }
        [JsonProperty("contactPersonName")]
        public string? ContactPersonName { get; set; }
        [JsonProperty("countryCode")]
        public string? CountryCode { get; set; }
        [JsonProperty("contactNumber")]
        public string? ContactNumber { get; set; }
        [JsonProperty("orderNumber")]
        public string? OrderNumber { get; set; }
        [JsonProperty("activationDate")]
        public string? ActivationDate { get; set; }
    }

    // Response DTOs (JsonProperty ensures API camelCase deserializes correctly)
    public class LoginResponse
    {
        [JsonProperty("accessToken")]
        public string AccessToken { get; set; } = string.Empty;
        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; } = string.Empty;
        [JsonProperty("expiresIn")]
        public int ExpiresIn { get; set; }
        [JsonProperty("user")]
        public UserInfo User { get; set; } = null!;
        [JsonProperty("licenseValid")]
        public bool LicenseValid { get; set; }
        [JsonProperty("license")]
        public LicenseInfo? License { get; set; }
        /// <summary>True on first successful login; show Update Password screen for corp user.</summary>
        [JsonProperty("firstTimeLogin")]
        public bool FirstTimeLogin { get; set; }
    }

    public class ChangePasswordRequest
    {
        [JsonProperty("currentPassword")]
        public string CurrentPassword { get; set; } = string.Empty;
        [JsonProperty("updatedPassword")]
        public string UpdatedPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordResponse
    {
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
        [JsonProperty("success")]
        public bool Success { get; set; }
    }

    public class UserInfo
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;
        [JsonProperty("licenseStatus")]
        public string LicenseStatus { get; set; } = string.Empty;
        [JsonProperty("trialEndsAt")]
        public DateTime? TrialEndsAt { get; set; }
        [JsonProperty("userType")]
        public string UserType { get; set; } = string.Empty;
    }

    public class LicenseInfo
    {
        [JsonProperty("key")]
        public string? Key { get; set; }
        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;
        [JsonProperty("maxDevices")]
        public int? MaxDevices { get; set; }
        [JsonProperty("planId")]
        public int? PlanId { get; set; }
        /// <summary>Plan name from backend (e.g. "1 Month", "12 Months"). Used to calculate remaining days when ExpiryDate is not sent.</summary>
        [JsonProperty("planName")]
        public string? PlanName { get; set; }
        [JsonProperty("trialEndsAt")]
        public DateTime? TrialEndsAt { get; set; }
        [JsonProperty("purchaseDate")]
        public DateTime? PurchaseDate { get; set; }
        [JsonProperty("expiryDate")]
        public DateTime? ExpiryDate { get; set; }
        /// <summary>Remaining trial detection attempts (max 30). Null for paid licenses.</summary>
        [JsonProperty("trialAttemptsRemaining")]
        public int? TrialAttemptsRemaining { get; set; }
    }

    public class RegisterResponse
    {
        public Guid UserId { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool RequiresEmailVerification { get; set; }
        public DateTime? TrialEndsAt { get; set; }
    }

    public class VerifyEmailRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class VerifyEmailResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public DateTime TrialEndsAt { get; set; }
    }

    public class ResendOtpRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ResendOtpResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    public class ForgotUsernameRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ForgotUsernameResponse
    {
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
        [JsonProperty("attemptsRemaining")]
        public int AttemptsRemaining { get; set; }
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ForgotPasswordResponse
    {
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
        [JsonProperty("attemptsRemaining")]
        public int AttemptsRemaining { get; set; }
    }

    public class VerifyPasswordResetOtpRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class VerifyPasswordResetOtpResponse
    {
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
        [JsonProperty("resetToken")]
        public string ResetToken { get; set; } = string.Empty;
        [JsonProperty("expiresAt")]
        public DateTime ExpiresAt { get; set; }
    }

    public class ResetPasswordRequest
    {
        [JsonProperty("resetToken")]
        public string ResetToken { get; set; } = string.Empty;
        [JsonProperty("newPassword")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ResetPasswordResponse
    {
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;
        [JsonProperty("success")]
        public bool Success { get; set; }
    }

    public class EmailVerificationEventArgs : EventArgs
    {
        public string Email { get; set; } = string.Empty;
    }

    // Error Response
    public class ApiErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
