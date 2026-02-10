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
    public class LicensePurchaseService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly TokenStorage _tokenStorage;

        public LicensePurchaseService()
        {
            _baseUrl = "https://xphy-web-c5e3v.ondigitalocean.app";
            /*_baseUrl = "https://localhost:7296";*/
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
        /// Initiates a license purchase by creating a payment intent
        /// </summary>
        public async Task<PurchaseResponse?> InitiatePurchaseAsync(int planId)
        {
            try
            {
                // Get access token
                var tokens = _tokenStorage.GetTokens();
                if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
                {
                    throw new Exception("User not authenticated. Please login first.");
                }

                // Set authorization header
                _httpClient.DefaultRequestHeaders.Remove("Authorization");
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokens.AccessToken}");

                var request = new PurchaseRequest
                {
                    PlanId = planId
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/License/purchase", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var purchaseResponse = JsonConvert.DeserializeObject<PurchaseResponse>(responseJson);
                    return purchaseResponse;
                }
                else
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                    throw new Exception(errorResponse?.Message ?? $"Purchase initiation failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Purchase initiation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Confirms the purchase after successful Stripe payment
        /// </summary>
        public async Task<PurchaseConfirmResponse> ConfirmPurchaseAsync(string paymentIntentId)
        {
            try
            {
                // Get access token
                var tokens = _tokenStorage.GetTokens();
                if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
                {
                    throw new Exception("User not authenticated. Please login first.");
                }

                // Set authorization header
                _httpClient.DefaultRequestHeaders.Remove("Authorization");
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokens.AccessToken}");

                var request = new PurchaseConfirmRequest
                {
                    PaymentIntentId = paymentIntentId
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/License/purchase/confirm", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var confirmResponse = JsonConvert.DeserializeObject<PurchaseConfirmResponse>(responseJson);
                    if (confirmResponse == null)
                    {
                        throw new Exception("Invalid response from server.");
                    }
                    return confirmResponse;
                }
                else
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                    throw new Exception(errorResponse?.Message ?? $"Purchase confirmation failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Purchase confirmation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Consume one trial detection attempt. Call when a Trial user starts a detection (audio or video).
        /// Returns Allowed and updated TrialAttemptsRemaining; for non-Trial users, Allowed is true and TrialAttemptsRemaining is null.
        /// </summary>
        public async Task<UseDetectionAttemptResponse> UseDetectionAttemptAsync()
        {
            var tokens = _tokenStorage.GetTokens();
            if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
            {
                return new UseDetectionAttemptResponse
                {
                    Allowed = false,
                    Message = "User not authenticated. Please login first."
                };
            }

            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokens.AccessToken}");

            try
            {
                var response = await _httpClient.PostAsync("/api/License/detection-attempt", new StringContent("", Encoding.UTF8, "application/json"));

                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    string? errorMessage = null;
                    try
                    {
                        var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(responseJson);
                        errorMessage = errorResponse?.Message;
                    }
                    catch { /* ignore */ }
                    return new UseDetectionAttemptResponse
                    {
                        Allowed = false,
                        Message = errorMessage ?? $"Server returned {(int)response.StatusCode}. Please try again."
                    };
                }

                if (string.IsNullOrWhiteSpace(responseJson))
                {
                    return new UseDetectionAttemptResponse
                    {
                        Allowed = false,
                        Message = "Server returned an empty response. Please try again."
                    };
                }

                var result = JsonConvert.DeserializeObject<UseDetectionAttemptResponse>(responseJson);
                if (result != null)
                    return result;

                return new UseDetectionAttemptResponse
                {
                    Allowed = false,
                    Message = "Server response was not in the expected format. Please try again."
                };
            }
            catch (TaskCanceledException)
            {
                return new UseDetectionAttemptResponse
                {
                    Allowed = false,
                    Message = "Request timed out. Please check your connection and try again."
                };
            }
            catch (HttpRequestException ex)
            {
                return new UseDetectionAttemptResponse
                {
                    Allowed = false,
                    Message = $"Network error: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new UseDetectionAttemptResponse
                {
                    Allowed = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// Validate license with device fingerprint. Returns current license info including TrialAttemptsRemaining.
        /// Use after detection ends to refresh the displayed attempt count.
        /// </summary>
        public async Task<ValidateResponse?> ValidateLicenseAsync()
        {
            var tokens = _tokenStorage.GetTokens();
            if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
                return null;

            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokens.AccessToken}");

            try
            {
                var fingerprintService = new DeviceFingerprintService();
                var request = new ValidateRequest
                {
                    DeviceFingerprint = fingerprintService.GetDeviceFingerprint()
                };
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/License/validate", content);
                var responseJson = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return null;
                var result = JsonConvert.DeserializeObject<ValidateResponse>(responseJson);
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
