#nullable enable
using System;
using System.IO;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    public class TokenStorage
    {
        private static readonly string TokenFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "X-PHY",
            "X-PHY Deepfake Detector",
            "tokens.json"
        );

        public class StoredTokens
        {
            public string AccessToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public Guid UserId { get; set; }
            public string Username { get; set; } = string.Empty;
            public UserInfo? UserInfo { get; set; }
            public LicenseInfo? LicenseInfo { get; set; }
        }

        public void SaveTokens(string accessToken, string refreshToken, int expiresIn, Guid userId, string username, UserInfo? userInfo = null, LicenseInfo? licenseInfo = null)
        {
            try
            {
                var directory = Path.GetDirectoryName(TokenFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var tokens = new StoredTokens
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn),
                    UserId = userId,
                    Username = username,
                    UserInfo = userInfo,
                    LicenseInfo = licenseInfo
                };

                string json = JsonConvert.SerializeObject(tokens, Formatting.Indented);
                File.WriteAllText(TokenFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving tokens: {ex.Message}");
            }
        }

        /// <summary>Returns stored tokens if present. Returns even when access token is expired so license display and API can be attempted (backend may return 401 if expired).</summary>
        public StoredTokens? GetTokens()
        {
            try
            {
                if (File.Exists(TokenFilePath))
                {
                    string json = File.ReadAllText(TokenFilePath);
                    var tokens = JsonConvert.DeserializeObject<StoredTokens>(json);
                    return tokens;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading tokens: {ex.Message}");
            }

            return null;
        }

        /// <summary>Returns true if stored access token is still valid (not expired).</summary>
        public bool IsAccessTokenValid()
        {
            var tokens = GetTokens();
            return tokens != null && !string.IsNullOrEmpty(tokens.AccessToken) && tokens.ExpiresAt > DateTime.UtcNow;
        }

        /// <summary>Updates only UserInfo and LicenseInfo (e.g. after purchase confirm). Keeps existing tokens.</summary>
        public void UpdateUserAndLicense(UserInfo? userInfo, LicenseInfo? licenseInfo)
        {
            try
            {
                var tokens = GetTokens();
                if (tokens == null) return;

                var expiresInSeconds = (int)Math.Max(0, (tokens.ExpiresAt - DateTime.UtcNow).TotalSeconds);
                SaveTokens(
                    tokens.AccessToken,
                    tokens.RefreshToken,
                    expiresInSeconds > 0 ? expiresInSeconds : 3600,
                    tokens.UserId,
                    tokens.Username,
                    userInfo ?? tokens.UserInfo,
                    licenseInfo ?? tokens.LicenseInfo
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating user/license: {ex.Message}");
            }
        }

        public void ClearTokens()
        {
            try
            {
                if (File.Exists(TokenFilePath))
                {
                    File.Delete(TokenFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing tokens: {ex.Message}");
            }
        }

        public bool IsAuthenticated()
        {
            var tokens = GetTokens();
            return tokens != null && !string.IsNullOrEmpty(tokens.AccessToken);
        }
    }
}
