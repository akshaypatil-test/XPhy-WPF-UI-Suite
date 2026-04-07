#nullable enable
using System;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    /// <summary>Gets user profile. Uses AuthenticatedApiClient so 401 triggers refresh and retry.</summary>
    public class UserProfileService
    {
        private readonly AuthenticatedApiClient _apiClient;

        public UserProfileService()
        {
            _apiClient = new AuthenticatedApiClient();
            try { ServicePointManager.ServerCertificateValidationCallback += (_, __, ___, ____) => true; } catch { }
        }

        public async Task<UserProfile?> GetProfileAsync()
        {
            try
            {
                var response = await _apiClient.GetAsync("/api/User/profile").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<UserProfile>(json);
                }
            }
            catch (SessionExpiredException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UserProfileService GetProfile: {ex.Message}");
            }

            return null;
        }

        /// <summary>Save email preference (true/false) to User on the server.</summary>
        public async Task<bool> UpdateEmailPreferencesAsync(bool emailPreferences)
        {
            try
            {
                var response = await _apiClient.PatchAsync("/api/User/profile/email-preferences", new { EmailPreferences = emailPreferences }).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (SessionExpiredException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UserProfileService UpdateEmailPreferences: {ex.Message}");
                return false;
            }
        }
    }
}
