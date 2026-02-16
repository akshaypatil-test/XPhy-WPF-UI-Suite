using System;

namespace x_phy_wpf_ui.Models
{
    /// <summary>
    /// Represents a single row in the Detection Results table.
    /// </summary>
    public class DetectionResultItem
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }  // "Video" or "Audio"
        public bool IsAiManipulationDetected { get; set; }
        public int ConfidencePercent { get; set; }
        public string ResultPathOrId { get; set; }  // Artifact path for loading evidence images (not shown in Media Source)
        /// <summary>Display text for "Media Source" (e.g. "Zoom", "Google Chrome"). When empty, UI may show "Local" or path.</summary>
        public string MediaSourceDisplay { get; set; }
        public int SerialNumber { get; set; }
        /// <summary>Duration in seconds, if available (e.g. from API). Display is capped at max detection length (60s).</summary>
        public decimal? DurationSeconds { get; set; }

        /// <summary>Max detection length in seconds; duration display is capped at this.</summary>
        private const int MaxDurationDisplaySeconds = 60;

        public string ResultText => IsAiManipulationDetected ? "AI Manipulation Detected" : "No AI Manipulation detected";
        public string ConfidenceText => ConfidencePercent + "%";
        public string DurationDisplay => DurationSeconds.HasValue && DurationSeconds.Value > 0
            ? $"{(int)Math.Min((decimal)MaxDurationDisplaySeconds, DurationSeconds.Value)} Seconds"
            : "—";
        public string DetectionTypeDisplay => Type == "Audio" ? "Audio" : (Type == "Video" ? "Conference Video" : Type);
    }
}
