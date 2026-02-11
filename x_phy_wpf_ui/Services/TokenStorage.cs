#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    public class TokenStorage
    {
        private static readonly string AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "X-PHY",
            "X-PHY Deepfake Detector"
        );

        private static readonly string TokenFilePath = Path.Combine(AppDataDirectory, "tokens.json");

        private static readonly string InstallIdentityFilePath = Path.Combine(AppDataDirectory, "install_identity.txt");

        /// <summary>
        /// Call once at app startup. If the current app install is different from the one that
        /// last saved tokens (e.g. after uninstall + reinstall), clears stored tokens so the user
        /// must log in again. Uses app directory creation time so a new install (new folder) is detected.
        /// </summary>
        public static void ClearTokensIfNewInstall()
        {
            try
            {
                string currentExePath = GetCurrentExePath();
                if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
                    return;

                string appDir = Path.GetDirectoryName(currentExePath);
                if (string.IsNullOrEmpty(appDir) || !Directory.Exists(appDir))
                    return;

                // Use app directory creation time: uninstall deletes the folder, reinstall creates a new one with a new creation time.
                DateTime appDirCreated = Directory.GetCreationTimeUtc(appDir);
                string normalizedPath = Path.GetFullPath(appDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string currentIdentity = normalizedPath + "\n" + appDirCreated.Ticks;

                if (!Directory.Exists(AppDataDirectory))
                {
                    Directory.CreateDirectory(AppDataDirectory);
                    File.WriteAllText(InstallIdentityFilePath, currentIdentity);
                    return;
                }

                if (!File.Exists(InstallIdentityFilePath))
                {
                    File.WriteAllText(InstallIdentityFilePath, currentIdentity);
                    return;
                }

                string storedIdentity = File.ReadAllText(InstallIdentityFilePath).Trim();
                if (string.Equals(storedIdentity, currentIdentity, StringComparison.Ordinal))
                    return;

                // Identity changed (reinstall or new app folder): clear tokens and update identity
                if (File.Exists(TokenFilePath))
                {
                    File.Delete(TokenFilePath);
                    System.Diagnostics.Debug.WriteLine("Tokens cleared: new install detected (app directory identity changed).");
                }

                File.WriteAllText(InstallIdentityFilePath, currentIdentity);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Install identity check failed: {ex.Message}");
            }
        }

        private static string GetCurrentExePath()
        {
            try
            {
                var path = System.Reflection.Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return Path.GetFullPath(path);
            }
            catch { }

            try
            {
                using var process = Process.GetCurrentProcess();
                var path = process.MainModule?.FileName ?? string.Empty;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return Path.GetFullPath(path);
            }
            catch { }

            try
            {
                var path = System.Reflection.Assembly.GetExecutingAssembly().Location ?? string.Empty;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return Path.GetFullPath(path);
            }
            catch { }

            return string.Empty;
        }

        public class StoredTokens
        {
            public string AccessToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public Guid UserId { get; set; }
            public string Username { get; set; } = string.Empty;
            public UserInfo? UserInfo { get; set; }
            public LicenseInfo? LicenseInfo { get; set; }
            /// <summary>True when user checked Remember Me at login; when false, logout on app close.</summary>
            public bool RememberMe { get; set; }
        }

        public void SaveTokens(string accessToken, string refreshToken, int expiresIn, Guid userId, string username, UserInfo? userInfo = null, LicenseInfo? licenseInfo = null, bool rememberMe = false)
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
                    LicenseInfo = licenseInfo,
                    RememberMe = rememberMe
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

        /// <summary>Updates only the access token and expiry (e.g. after refresh). Keeps refresh token, user, license, RememberMe.</summary>
        public void UpdateAccessToken(string accessToken, int expiresIn)
        {
            try
            {
                var tokens = GetTokens();
                if (tokens == null) return;
                var expiresInSeconds = (int)Math.Max(0, (tokens.ExpiresAt - DateTime.UtcNow).TotalSeconds);
                SaveTokens(
                    accessToken,
                    tokens.RefreshToken,
                    expiresIn,
                    tokens.UserId,
                    tokens.Username,
                    tokens.UserInfo,
                    tokens.LicenseInfo,
                    tokens.RememberMe
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating access token: {ex.Message}");
            }
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
                    licenseInfo ?? tokens.LicenseInfo,
                    tokens.RememberMe
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
