namespace x_phy_wpf_ui.Models
{
    /// <summary>Response from GET /api/update/check (matches licensing API).</summary>
    public class UpdateCheckResponse
    {
        public bool IsUpdateAvailable { get; set; }
        public string LatestVersion { get; set; }
        public string DownloadUrl { get; set; }
        public bool Mandatory { get; set; }
        public string ReleaseNotes { get; set; }
    }
}
