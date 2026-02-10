#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    public class LicensePlanService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public LicensePlanService()
        {
            _baseUrl = "https://xphy-web-c5e3v.ondigitalocean.app";
            /* _baseUrl = "https://localhost:7296";*/
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Ignore SSL certificate errors for localhost (development only)
            ServicePointManager.ServerCertificateValidationCallback += 
                (sender, certificate, chain, sslPolicyErrors) => true;
        }

        public async Task<List<LicensePlanDto>> GetPlansAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/License/plans");

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var plansResponse = JsonConvert.DeserializeObject<LicensePlansResponse>(responseJson);
                    
                    if (plansResponse?.Plans != null)
                    {
                        // Filter out Trial Plan and return only active plans
                        return plansResponse.Plans
                            .Where(p => !p.Name.Equals("Trial Plan", StringComparison.OrdinalIgnoreCase) &&
                                       !p.Name.Equals("Trial", StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                }
                else
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to fetch plans: {response.StatusCode}");
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
                throw new Exception($"Error fetching plans: {ex.Message}");
            }

            return new List<LicensePlanDto>();
        }
    }
}
