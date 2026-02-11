#nullable enable
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    public class LicensePurchaseService
    {
        private readonly AuthenticatedApiClient _apiClient;

        public LicensePurchaseService()
        {
            _apiClient = new AuthenticatedApiClient();
        }

        /// <summary>
        /// Initiates a license purchase by creating a payment intent. On session expiry, throws SessionExpiredException.
        /// </summary>
        public async Task<PurchaseResponse?> InitiatePurchaseAsync(int planId)
        {
            try
            {
                var request = new PurchaseRequest { PlanId = planId };
                var response = await _apiClient.PostAsync("/api/License/purchase", request).ConfigureAwait(false);
                var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<PurchaseResponse>(responseJson);
                }
                var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(responseJson);
                throw new Exception(errorResponse?.Message ?? $"Purchase initiation failed: {response.StatusCode}");
            }
            catch (SessionExpiredException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex) when (ex is not SessionExpiredException)
            {
                throw new Exception($"Purchase initiation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Confirms the purchase after successful Stripe payment. On session expiry, throws SessionExpiredException.
        /// </summary>
        public async Task<PurchaseConfirmResponse> ConfirmPurchaseAsync(string paymentIntentId)
        {
            try
            {
                var request = new PurchaseConfirmRequest { PaymentIntentId = paymentIntentId };
                var response = await _apiClient.PostAsync("/api/License/purchase/confirm", request).ConfigureAwait(false);
                var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var confirmResponse = JsonConvert.DeserializeObject<PurchaseConfirmResponse>(responseJson);
                    if (confirmResponse == null) throw new Exception("Invalid response from server.");
                    return confirmResponse;
                }
                var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(responseJson);
                throw new Exception(errorResponse?.Message ?? $"Purchase confirmation failed: {response.StatusCode}");
            }
            catch (SessionExpiredException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex) when (ex is not SessionExpiredException)
            {
                throw new Exception($"Purchase confirmation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Consume one trial detection attempt. On session expiry, throws SessionExpiredException.
        /// </summary>
        public async Task<UseDetectionAttemptResponse> UseDetectionAttemptAsync()
        {
            try
            {
                var response = await _apiClient.PostAsync("/api/License/detection-attempt", new { }).ConfigureAwait(false);
                var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    string? errorMessage = null;
                    try { errorMessage = JsonConvert.DeserializeObject<ApiErrorResponse>(responseJson)?.Message; } catch { }
                    return new UseDetectionAttemptResponse { Allowed = false, Message = errorMessage ?? $"Server returned {(int)response.StatusCode}. Please try again." };
                }
                if (string.IsNullOrWhiteSpace(responseJson))
                    return new UseDetectionAttemptResponse { Allowed = false, Message = "Server returned an empty response. Please try again." };
                var result = JsonConvert.DeserializeObject<UseDetectionAttemptResponse>(responseJson);
                return result ?? new UseDetectionAttemptResponse { Allowed = false, Message = "Server response was not in the expected format. Please try again." };
            }
            catch (SessionExpiredException)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                return new UseDetectionAttemptResponse { Allowed = false, Message = "Request timed out. Please check your connection and try again." };
            }
            catch (HttpRequestException ex)
            {
                return new UseDetectionAttemptResponse { Allowed = false, Message = $"Network error: {ex.Message}" };
            }
            catch (Exception ex)
            {
                return new UseDetectionAttemptResponse { Allowed = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// Validate license with device fingerprint. On session expiry, throws SessionExpiredException.
        /// </summary>
        public async Task<ValidateResponse?> ValidateLicenseAsync()
        {
            try
            {
                var fingerprintService = new DeviceFingerprintService();
                var request = new ValidateRequest { DeviceFingerprint = fingerprintService.GetDeviceFingerprint() };
                var response = await _apiClient.PostAsync("/api/License/validate", request).ConfigureAwait(false);
                var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;
                return JsonConvert.DeserializeObject<ValidateResponse>(responseJson);
            }
            catch (SessionExpiredException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }
    }
}
