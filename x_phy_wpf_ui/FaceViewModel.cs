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
    }
}
