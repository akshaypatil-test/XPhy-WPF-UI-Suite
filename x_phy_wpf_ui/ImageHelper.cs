using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XPhyWrapper;

namespace x_phy_wpf_ui
{
    /// <summary>
    /// Helper class to convert DetectedFace image data to WPF BitmapSource and to combine multiple face images into a grid.
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

            PixelFormat pixelFormat = PixelFormats.Bgr24;
            int stride = (face.ImageWidth * pixelFormat.BitsPerPixel + 7) / 8;

            BitmapSource bitmap = BitmapSource.Create(
                face.ImageWidth,
                face.ImageHeight,
                96, 96,
                pixelFormat,
                null,
                face.ImageData,
                stride);

            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// Combines multiple face images into a single grid image (2 columns when more than one face),
        /// matching the layout used in x_phy_detection_program_ui (vision::utils::Grid). Returns null if no valid images.
        /// </summary>
        public static BitmapSource CombineFaceImagesIntoGrid(IEnumerable<DetectedFace> faces)
        {
            if (faces == null) return null;
            var list = faces.Where(f => f.ImageData != null && f.ImageData.Length > 0).ToList();
            if (list.Count == 0) return null;
            if (list.Count == 1) return ConvertToBitmapSource(list[0]);

            var bitmaps = new List<BitmapSource>();
            foreach (var face in list)
            {
                var bmp = ConvertToBitmapSource(face);
                if (bmp != null) bitmaps.Add(bmp);
            }
            if (bitmaps.Count == 0) return null;
            if (bitmaps.Count == 1) return bitmaps[0];

            return CombineBitmapSourcesIntoGrid(bitmaps);
        }

        /// <summary>
        /// Arranges multiple BitmapSources in a 2-column grid (same layout as C++ Grid: 2 cols when > 1 cell).
        /// </summary>
        public static BitmapSource CombineBitmapSourcesIntoGrid(IList<BitmapSource> sources)
        {
            if (sources == null || sources.Count == 0) return null;
            if (sources.Count == 1) return sources[0];

            int cellWidth = sources[0].PixelWidth;
            int cellHeight = sources[0].PixelHeight;
            int cols = 2;
            int rows = (sources.Count + cols - 1) / cols;
            int totalWidth = cellWidth * cols;
            int totalHeight = cellHeight * rows;

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    int c = i % cols;
                    int r = i / cols;
                    dc.DrawImage(sources[i], new Rect(c * cellWidth, r * cellHeight, cellWidth, cellHeight));
                }
            }

            var renderBitmap = new RenderTargetBitmap(totalWidth, totalHeight, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(visual);
            renderBitmap.Freeze();
            return renderBitmap;
        }
    }
}
