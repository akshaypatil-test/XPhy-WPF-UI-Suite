using System;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui
{
    /// <summary>Event data when sign-in API succeeds. Carries license key and full login response so the app can validate with Keygen first, then save tokens and complete login only if valid.</summary>
    public class SignInSuccessfulEventArgs : EventArgs
    {
        /// <summary>License key from the login response or (for corporate) from the input box. Used to write config.toml and run native Keygen validation.</summary>
        public string? LicenseKey { get; }

        /// <summary>Full login response. Saved to tokens only after native license validation succeeds.</summary>
        public LoginResponse? LoginResponse { get; }

        /// <summary>True when raised from Corporate Sign-In; used to return to corporate sign-in screen on validation error.</summary>
        public bool FromCorporateSignIn { get; }

        /// <summary>True when user checked Remember Me at login; used for refresh token expiry and logout-on-close behavior.</summary>
        public bool RememberMe { get; }

        public SignInSuccessfulEventArgs(string? licenseKey, LoginResponse? loginResponse, bool fromCorporateSignIn = false, bool rememberMe = false)
        {
            LicenseKey = licenseKey;
            LoginResponse = loginResponse;
            FromCorporateSignIn = fromCorporateSignIn;
            RememberMe = rememberMe;
        }
    }
}
