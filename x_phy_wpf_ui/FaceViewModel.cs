using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace x_phy_wpf_ui
{
    /// <summary>
    /// View model for displaying a detected face
    /// </summary>
    public class FaceViewModel
    {
        public BitmapSource Image { get; set; }
        public string StatusText { get; set; }
        public Brush StatusColor { get; set; }
        public string PercentageText { get; set; }
        /// <summary>Model confidence that this face is fake (0–100). Used for notification "Confidence %".</summary>
        public double ConfidencePercent { get; set; }
    }
}
