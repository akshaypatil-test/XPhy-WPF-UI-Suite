#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    public class UserProfileService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly TokenStorage _tokenStorage;

        public UserProfileService()
        {
            _baseUrl = "http://localhost:5163";
            /*_baseUrl = "https://xphy-web-c5e3v.ondigitalocean.app";*/
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _tokenStorage = new TokenStorage();
            ServicePointManager.ServerCertificateValidationCallback += (_, __, ___, ____) => true;
        }

        public async Task<UserProfile?> GetProfileAsync()
        {
            var tokens = _tokenStorage.GetTokens();
            if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
                return null;

            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokens.AccessToken}");

            try
            {
                var response = await _httpClient.GetAsync("/api/User/profile");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<UserProfile>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UserProfileService GetProfile: {ex.Message}");
            }

            return null;
        }
    }
}
