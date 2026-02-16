#nullable enable
using System;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    /// <summary>
    /// Calls the backend Result API: CreateResult (POST) and GetResults (GET).
    /// Uses AuthenticatedApiClient so 401 triggers refresh and retry.
    /// </summary>
    public class ResultsApiService
    {
        private readonly AuthenticatedApiClient _apiClient;

        public ResultsApiService()
        {
            _apiClient = new AuthenticatedApiClient();
            try
            {
                ServicePointManager.ServerCertificateValidationCallback += (_, __, ___, ____) => true;
            }
            catch { }
        }

        /// <summary>POST api/Result - create a detection result for the current user.</summary>
        public async Task<CreateResultResponse?> CreateResultAsync(CreateResultRequest request)
        {
            try
            {
                var response = await _apiClient.PostAsync("/api/Result", request).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var result = JsonConvert.DeserializeObject<CreateResultResponse>(responseJson);
                    System.Diagnostics.Debug.WriteLine($"ResultsApiService CreateResult: 201 Created, Id={result?.Id}");
                    return result;
                }
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"ResultsApiService CreateResult: {(int)response.StatusCode} {response.ReasonPhrase} -> {body}");
            }
            catch (SessionExpiredException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResultsApiService CreateResult failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>GET api/Result - get detection results for the current user, optionally filtered by machineFingerprint.</summary>
        public async Task<GetResultsResponse?> GetResultsAsync()
        {
            string? machineFingerprint = null;
            try { machineFingerprint = new DeviceFingerprintService().GetDeviceFingerprint(); } catch { }

            var path = string.IsNullOrEmpty(machineFingerprint)
                ? "/api/Result"
                : "/api/Result?machineFingerprint=" + Uri.EscapeDataString(machineFingerprint);

            try
            {
                var response = await _apiClient.GetAsync(path).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<GetResultsResponse>(responseJson);
                }
            }
            catch (SessionExpiredException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ResultsApiService GetResults failed: {ex.Message}");
            }

            return null;
        }
    }
}
