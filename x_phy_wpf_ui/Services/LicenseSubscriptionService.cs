#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    public class LicenseSubscriptionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly TokenStorage _tokenStorage;

        public LicenseSubscriptionService()
        {
            _baseUrl = "http://localhost:5163";
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _tokenStorage = new TokenStorage();
            ServicePointManager.ServerCertificateValidationCallback += (_, __, ___, ____) => true;
        }

        public async Task<List<UserLicenseItem>?> GetMyLicensesAsync()
        {
            var tokens = _tokenStorage.GetTokens();
            if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
                return null;

            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokens.AccessToken}");

            try
            {
                var response = await _httpClient.GetAsync("/api/User/licenses");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var obj = JsonConvert.DeserializeObject<UserLicensesResponse>(json);
                    return obj?.Licenses ?? new List<UserLicenseItem>();
                }
            }
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
