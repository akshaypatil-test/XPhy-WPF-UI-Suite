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
        private readonly TokenStorage _tokenStorage;

        public LicensePlanService()
        {
            //_baseUrl = "http://localhost:5163";
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

        /// <summary>PlanType for API: CorpUser or NonCorpUser. Matches backend XPhy.Licensing.Api.Models.PlanType.</summary>
        private string GetPlanTypeForApi()
        {
            var tokens = _tokenStorage.GetTokens();
            var userType = tokens?.UserInfo?.UserType?.Trim();
            if (string.Equals(userType, "Corp", StringComparison.OrdinalIgnoreCase))
                return "CorpUser";
            return "NonCorpUser";
        }

        public async Task<List<LicensePlanDto>> GetPlansAsync()
        {
            try
            {
                var planType = GetPlanTypeForApi();
                var response = await _httpClient.GetAsync($"/api/License/plans?planType={Uri.EscapeDataString(planType)}");

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var plansResponse = JsonConvert.DeserializeObject<LicensePlansResponse>(responseJson);
                    
                    if (plansResponse?.Plans != null)
                    {
                        // Filter out Trial, then sort order: 1-Month, 3-Month, 6-Month, 12-Month (match Figma)
                        var filtered = plansResponse.Plans
                            .Where(p => !p.Name.Equals("Trial Plan", StringComparison.OrdinalIgnoreCase) &&
                                       !p.Name.Equals("Trial", StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        return SortPlansByDisplayOrder(filtered);
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

        /// <summary>Display order per Figma: 1-Month, 3-Month, 6-Month, 12-Month, then others.</summary>
        private static List<LicensePlanDto> SortPlansByDisplayOrder(List<LicensePlanDto> plans)
        {
            var order = new[] { "1-Month", "1 Month", "3-Month", "3 Month", "6-Month", "6 Month", "12-Month", "12 Month" };
            return plans
                .OrderBy(p =>
                {
                    var name = (p.Name ?? "").Trim();
                    for (int i = 0; i < order.Length; i++)
                        if (name.Equals(order[i], StringComparison.OrdinalIgnoreCase))
                            return i;
                    return order.Length;
                })
                .ToList();
        }
    }
}
