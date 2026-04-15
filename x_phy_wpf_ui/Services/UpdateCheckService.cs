using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    /// <summary>Calls the licensing API update check (no auth required).</summary>
    public class UpdateCheckService
    {
        private readonly HttpClient _httpClient;

        public UpdateCheckService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(ApiBaseUrl),
                Timeout = TimeSpan.FromSeconds(30),
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>Same host as AuthService / AuthenticatedApiClient.</summary>
        public static string ApiBaseUrl => "https://deepfakedetector.x-phy.com";

        public async Task<UpdateCheckResult> CheckAsync(string currentVersion)
        {
            if (string.IsNullOrWhiteSpace(currentVersion))
                return UpdateCheckResult.Fail("Current version is not available.");

            var q = Uri.EscapeDataString(currentVersion.Trim());
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync("/api/update/check?currentVersion=" + q).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return UpdateCheckResult.Fail("Could not reach the update server. " + ex.Message);
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Fail(
                    string.IsNullOrWhiteSpace(body)
                        ? "Update check failed (" + (int)response.StatusCode + ")."
                        : body
                );
            }

            try
            {
                var dto = JsonConvert.DeserializeObject<UpdateCheckResponse>(body);
                if (dto == null)
                    return UpdateCheckResult.Fail("Invalid response from update server.");

                return new UpdateCheckResult { Ok = true, Response = dto };
            }
            catch (Exception ex)
            {
                return UpdateCheckResult.Fail("Could not read update information. " + ex.Message);
            }
        }
    }

    public class UpdateCheckResult
    {
        public bool Ok { get; set; }
        public UpdateCheckResponse Response { get; set; }
        public string ErrorMessage { get; set; }

        public static UpdateCheckResult Fail(string message)
        {
            return new UpdateCheckResult { Ok = false, ErrorMessage = message };
        }
    }
}
