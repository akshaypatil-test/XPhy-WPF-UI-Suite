using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XPhyWrapper;

namespace x_phy_wpf_ui
{
    /// <summary>
    /// Helper class to convert DetectedFace image data to WPF BitmapSource
    /// </summary>
    public static class ImageHelper
    {
        /// <summary>
        /// Converts DetectedFace image data (BGR format) to WPF BitmapSource
        /// </summary>
        public static BitmapSource ConvertToBitmapSource(DetectedFace face)
        {
            if (face.ImageData == null || face.ImageData.Length == 0)
                return null;

            // OpenCV uses BGR format, WPF uses RGB/BGR depending on PixelFormat
            // We'll use Bgr24 format which matches OpenCV's BGR
            PixelFormat pixelFormat = PixelFormats.Bgr24;
            int stride = (face.ImageWidth * pixelFormat.BitsPerPixel + 7) / 8;

            // Create BitmapSource from byte array
            BitmapSource bitmap = BitmapSource.Create(
                face.ImageWidth,
                face.ImageHeight,
                96, // DPI X
                96, // DPI Y
                pixelFormat,
                null, // Palette
                face.ImageData,
                stride);

            // Freeze to make it thread-safe and improve performance
            bitmap.Freeze();
            return bitmap;
        }
    }
}
