#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    /// <summary>Gets user licenses. Uses AuthenticatedApiClient so 401 triggers refresh and retry.</summary>
    public class LicenseSubscriptionService
    {
        private readonly AuthenticatedApiClient _apiClient;

        public LicenseSubscriptionService()
        {
            _apiClient = new AuthenticatedApiClient();
            try { ServicePointManager.ServerCertificateValidationCallback += (_, __, ___, ____) => true; } catch { }
        }

        public async Task<List<UserLicenseItem>?> GetMyLicensesAsync()
        {
            try
            {
                var response = await _apiClient.GetAsync("/api/User/licenses").ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var obj = JsonConvert.DeserializeObject<UserLicensesResponse>(json);
                    return obj?.Licenses ?? new List<UserLicenseItem>();
                }
            }
            catch (SessionExpiredException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LicenseSubscriptionService GetMyLicenses: {ex.Message}");
            }

            return null;
        }
    }

    public class UserLicensesResponse
    {
        [JsonProperty("licenses")]
        public List<UserLicenseItem> Licenses { get; set; } = new();
    }
}
