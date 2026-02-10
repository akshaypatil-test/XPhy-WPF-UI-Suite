using System;
using System.Windows.Media;

namespace x_phy_wpf_ui
{
    /// <summary>Event data when a deepfake is detected, for the notification popup.</summary>
    public class DeepfakeDetectedEventArgs : EventArgs
    {
        public int ConfidencePercent { get; }
        public string ResultPath { get; }
        public ImageSource EvidenceImageLeft { get; }
        public ImageSource EvidenceImageRight { get; }

        public DeepfakeDetectedEventArgs(int confidencePercent, string resultPath,
            ImageSource evidenceImageLeft = null, ImageSource evidenceImageRight = null)
        {
            ConfidencePercent = Math.Max(0, Math.Min(100, confidencePercent));
            ResultPath = resultPath ?? "";
            EvidenceImageLeft = evidenceImageLeft;
            EvidenceImageRight = evidenceImageRight;
        }
    }
}
