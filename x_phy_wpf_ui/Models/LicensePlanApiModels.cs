#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace x_phy_wpf_ui.Models
{
    public class LicensePlansResponse
    {
        public List<LicensePlanDto> Plans { get; set; } = new();
    }

    public class LicensePlanDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>Backend may return plan identifier as "planId" instead of "id". Use EffectivePlanId when sending to purchase API.</summary>
        [JsonProperty("planId")]
        public int PlanId { get; set; }

        public string Name { get; set; } = string.Empty;
        public int MaxDevices { get; set; }
        public decimal Price { get; set; }

        /// <summary>Returns the plan id from API (whichever of Id or PlanId was set).</summary>
        public int EffectivePlanId => Id != 0 ? Id : PlanId;
    }
}
