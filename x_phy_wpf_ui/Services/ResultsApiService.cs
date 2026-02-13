#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    /// <summary>
    /// Calls the backend Result API: CreateResult (POST) and GetResults (GET).
    /// Uses the same base URL and Bearer token as other app API calls.
    /// </summary>
    public class ResultsApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly TokenStorage _tokenStorage;

        public ResultsApiService()
        {
            /* _baseUrl = "http://localhost:5163";*/
            _baseUrl = "https://xphy-web-c5e3v.ondigitalocean.app";
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _tokenStorage = new TokenStorage();

            // Ignore SSL certificate errors for localhost (development only)
            ServicePointManager.ServerCertificateValidationCallback +=
                (sender, certificate, chain, sslPolicyErrors) => true;
        }

        /// <summary>
        /// POST api/Result - create a detection result for the current user.
        /// </summary>
        public async Task<CreateResultResponse?> CreateResultAsync(CreateResultRequest request)
        {
            var tokens = _tokenStorage.GetTokens();
            if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
            {
                System.Diagnostics.Debug.WriteLine("ResultsApiService CreateResult: No token (user not logged in?), skipping.");
                return null;
            }

            if (_httpClient.DefaultRequestHeaders.Contains("Authorization"))
                _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokens.AccessToken}");

            var url = _baseUrl.TrimEnd('/') + "/api/Result";
            try
            {
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                System.Diagnostics.Debug.WriteLine($"ResultsApiService CreateResult: POST {url}");
                var response = await _httpClient.PostAsync("/api/Result", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<CreateResultResponse>(responseJson);
                    System.Diagnostics.Debug.WriteLine($"ResultsApiService CreateResult: 201 Created, Id={result?.Id}");
                    return result;
                }
                var body = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"ResultsApiService CreateResult: {(int)response.StatusCode} {response.ReasonPhrase} -> {body}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResultsApiService CreateResult failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// GET api/Result - get detection results for the current user, filtered by current machine when machineFingerprint is sent.
        /// </summary>
        public async Task<GetResultsResponse?> GetResultsAsync()
        {
            var tokens = _tokenStorage.GetTokens();
            if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
                return null;

            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokens.AccessToken}");

            string machineFingerprint = null;
            try { machineFingerprint = new DeviceFingerprintService().GetDeviceFingerprint(); } catch { }

            try
            {
                var url = string.IsNullOrEmpty(machineFingerprint)
                    ? "/api/Result"
                    : "/api/Result?machineFingerprint=" + Uri.EscapeDataString(machineFingerprint);
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<GetResultsResponse>(responseJson);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResultsApiService GetResults failed: {ex.Message}");
            }

            return null;
        }
    }
}
