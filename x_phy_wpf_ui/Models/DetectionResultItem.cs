using System;
using System.Collections.Generic;

namespace x_phy_wpf_ui.Models
{
    /// <summary>
    /// Represents a single row in the Detection Results table.
    /// </summary>
    public class DetectionResultItem
    {
        /// <summary>Timestamp as stored (UTC from API/DB, or local from current run/local DB). Use TimestampLocal for display.</summary>
        public DateTime Timestamp { get; set; }
        /// <summary>Timestamp in system local time for display (converts from UTC when applicable).</summary>
        public DateTime TimestampLocal => Timestamp.Kind == DateTimeKind.Utc ? Timestamp.ToLocalTime() : Timestamp;
        /// <summary>Formatted for UI: DD/MM/YYYY, HH:mm in local time.</summary>
        public string TimestampDisplay => TimestampLocal.ToString("dd/MM/yyyy, HH:mm");
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

        /// <summary>Conference/calling apps: show "Conference Video" / "Conference Audio". All others (Chrome, VLC, etc.): "Web Stream Video" / "Web Stream Audio".</summary>
        private static readonly HashSet<string> ConferenceSourceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Zoom", "Microsoft Teams", "Microsoft Teams Chat", "Google Chat", "Google Meet",
            "Skype", "Cisco Webex", "Slack", "Discord", "BlueJeans", "GoTo Meeting", "JioMeet"
        };

        public string ResultText => IsAiManipulationDetected ? "AI Manipulation Detected" : "No AI Manipulation detected";
        /// <summary>Confidence for display. Audio has no score from native layer, so show "—" when detected and 0.</summary>
        public string ConfidenceText => (Type != null && Type.IndexOf("Audio", StringComparison.OrdinalIgnoreCase) >= 0 && IsAiManipulationDetected && ConfidencePercent == 0)
            ? "—"
            : (ConfidencePercent + "%");
        public string DurationDisplay => DurationSeconds.HasValue && DurationSeconds.Value > 0
            ? $"{(int)Math.Min((decimal)MaxDurationDisplaySeconds, DurationSeconds.Value)} Seconds"
            : "—";
        public string DetectionTypeDisplay => GetDetectionTypeDisplay(Type, MediaSourceDisplay);

        private static string GetDetectionTypeDisplay(string type, string mediaSourceDisplay)
        {
            if (string.IsNullOrEmpty(type)) return type ?? "";
            bool isConference = !string.IsNullOrEmpty(mediaSourceDisplay) && ConferenceSourceNames.Contains(mediaSourceDisplay.Trim());
            if (type.Equals("Video", StringComparison.OrdinalIgnoreCase))
                return isConference ? "Conference Video" : "Web Stream Video";
            if (type.Equals("Audio", StringComparison.OrdinalIgnoreCase))
                return isConference ? "Conference Audio" : "Web Stream Audio";
            return type;
        }
    }
}
