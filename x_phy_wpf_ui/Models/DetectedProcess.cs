using System;

namespace x_phy_wpf_ui.Models
{
    public class DetectedProcess
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ProcessType { get; set; } = string.Empty; // "VideoCalling", "MediaPlayer", "Browser"
        public bool HasYouTubeTab { get; set; }
        public string WindowTitle { get; set; } = string.Empty;
    }
}
