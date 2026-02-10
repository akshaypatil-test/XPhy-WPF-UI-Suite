#nullable enable
namespace x_phy_wpf_ui.Models
{
    /// <summary>
    /// Response from the detection-attempt API. Used when a Trial user consumes one attempt (audio/video detection).
    /// </summary>
    public class UseDetectionAttemptResponse
    {
        /// <summary>Whether a detection attempt was consumed and the client may proceed with detection.</summary>
        public bool Allowed { get; set; }

        /// <summary>Remaining trial detection attempts after this call. Null for paid licenses.</summary>
        public int? TrialAttemptsRemaining { get; set; }

        /// <summary>Message when not allowed (e.g. no trial attempts left).</summary>
        public string? Message { get; set; }
    }
}
