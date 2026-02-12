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
    /// Sends HTTP requests with Bearer token. On 401, tries to refresh the access token and retries once.
    /// If refresh fails, clears tokens, raises SessionExpired, and throws SessionExpiredException.
    /// </summary>
    public class AuthenticatedApiClient
    {
        public static event EventHandler? SessionExpired;

        private readonly string _baseUrl;
        private readonly TokenStorage _tokenStorage;
        private readonly AuthService _authService;
        private readonly HttpClient _httpClient;

        public AuthenticatedApiClient()
        {
            // _baseUrl = "http://localhost:5163";
            _baseUrl = "https://xphy-web-c5e3v.ondigitalocean.app";
            _tokenStorage = new TokenStorage();
            _authService = new AuthService();
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            try
            {
                ServicePointManager.ServerCertificateValidationCallback += (_, _, _, _) => true;
            }
            catch { }
        }

        /// <summary>POST with JSON body. On 401, refreshes token and retries once. Throws SessionExpiredException if refresh fails.</summary>
        public async Task<HttpResponseMessage> PostAsync(string path, object? body)
        {
            return await SendWithAuthAsync(HttpMethod.Post, path, body).ConfigureAwait(false);
        }

        /// <summary>GET. On 401, refreshes token and retries once. Throws SessionExpiredException if refresh fails.</summary>
        public async Task<HttpResponseMessage> GetAsync(string path)
        {
            return await SendWithAuthAsync(HttpMethod.Get, path, null).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> SendWithAuthAsync(HttpMethod method, string path, object? body)
        {
            var tokens = _tokenStorage.GetTokens();
            if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
            {
                TriggerSessionExpired();
                throw new SessionExpiredException();
            }

            var request = CreateRequest(method, path, body);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + tokens.AccessToken);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                return response;

            // 401: try refresh and retry once
            var refreshResponse = await _authService.RefreshTokenAsync(tokens.RefreshToken).ConfigureAwait(false);
            if (refreshResponse == null || string.IsNullOrEmpty(refreshResponse.AccessToken))
            {
                _tokenStorage.ClearTokens();
                SessionExpired?.Invoke(this, EventArgs.Empty);
                throw new SessionExpiredException();
            }

            _tokenStorage.UpdateAccessToken(refreshResponse.AccessToken, refreshResponse.ExpiresIn);

            var retryRequest = CreateRequest(method, path, body);
            retryRequest.Headers.TryAddWithoutValidation("Authorization", "Bearer " + refreshResponse.AccessToken);
            return await _httpClient.SendAsync(retryRequest).ConfigureAwait(false);
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body)
        {
            var request = new HttpRequestMessage(method, path);
            if (body != null && (method == HttpMethod.Post || method == HttpMethod.Put))
            {
                var json = JsonConvert.SerializeObject(body);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            return request;
        }

        private static void TriggerSessionExpired()
        {
            SessionExpired?.Invoke(null, EventArgs.Empty);
        }
    }
}
