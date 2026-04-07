using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace InstallerUI.Views
{
    public partial class LicenseView : UserControl
    {
        public LicenseView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object s, RoutedEventArgs e)
        {
            LoadEulaContent();
            // Reset so I Accept is disabled until user scrolls to bottom (avoids stale state when returning to this step)
            if (DataContext is InstallerViewModel vm)
                vm.EulaScrolledToBottom = false;
            // Re-check after layout (only enables if content fits without scroll or user scrolls to bottom)
            Dispatcher.BeginInvoke(new Action(CheckScrolledToBottom), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void LoadEulaContent()
        {
            // Try EULA from dependencies (e.g. X-PHY_DFD_EULA content); fallback to EULA.rtf or embedded text.
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var paths = new[]
            {
                Path.Combine(baseDir, "EULA.txt"),
                Path.Combine(baseDir, "EULA.rtf"),
                Path.Combine(baseDir, "..", "..", "..", "dependencies", "eula", "EULA.rtf")
            };
            foreach (var path in paths)
            {
                var full = Path.GetFullPath(path);
                if (!File.Exists(full)) continue;
                try
                {
                    var text = File.ReadAllText(full);
                    if (full.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase))
                        text = StripRtf(text);
                    LicenseText.Text = text;
                    return;
                }
                catch { /* try next */ }
            }
            LicenseText.Text = GetDefaultEulaText();
        }

        private static string StripRtf(string rtf)
        {
            if (string.IsNullOrEmpty(rtf)) return "";
            var t = Regex.Replace(rtf, @"\\[a-z]+\d*\s?", " ");
            t = Regex.Replace(t, @"[{}]", "");
            t = Regex.Replace(t, @"\s+", " ").Trim();
            return t.Length > 0 ? t : "X-PHY Deepfake Detector - End User License Agreement. See the full EULA in the application folder after installation.";
        }

        private static string GetDefaultEulaText()
        {
            return @"X-PHY DEEPFAKE DETECTOR - END USER LICENSE AGREEMENT

This End User License Agreement (""EULA"") is a legal agreement between you and X-PHY for the use of the X-PHY Deepfake Detector software product (""Software""). By installing or using the Software, you agree to be bound by the terms of this EULA.

1. LICENSE GRANT. Subject to the terms of this EULA, X-PHY grants you a limited, non-exclusive, non-transferable license to install and use the Software for your internal business or personal use.

2. RESTRICTIONS. You may not copy, modify, distribute, sell, or lease the Software or any part of it. You may not reverse engineer, decompile, or disassemble the Software except to the extent permitted by applicable law.

3. INTELLECTUAL PROPERTY. The Software is licensed, not sold. X-PHY retains all right, title, and interest in and to the Software and any copies thereof.

4. DISCLAIMER. The Software is provided ""AS IS"" without warranty of any kind. X-PHY disclaims all warranties, express or implied.

5. LIMITATION OF LIABILITY. In no event shall X-PHY be liable for any indirect, incidental, special, or consequential damages arising out of or in connection with the use of the Software.

6. TERMINATION. This EULA is effective until terminated. Your rights under this EULA will terminate automatically without notice if you fail to comply with any term of this EULA.";
        }

        private void EulaScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            CheckScrolledToBottom();
        }

        private void CheckScrolledToBottom()
        {
            if (DataContext is not InstallerViewModel vm) return;
            var sv = EulaScrollViewer;
            if (sv == null) return;

            // Don't enable until layout is valid (avoids ExtentHeight/ViewportHeight being 0 on first load)
            if (sv.ExtentHeight <= 0 || sv.ViewportHeight <= 0)
                return;

            const double tolerance = 10;
            // At bottom = content fits without scrolling, OR user has scrolled to the bottom
            var contentFitsWithoutScroll = sv.ExtentHeight <= sv.ViewportHeight + tolerance;
            var userScrolledToBottom = sv.VerticalOffset + sv.ViewportHeight >= sv.ExtentHeight - tolerance;
            var atBottom = contentFitsWithoutScroll || userScrolledToBottom;

            vm.EulaScrolledToBottom = atBottom;
        }

        /// <summary>Called from MainWindow footer Print link. Prints the EULA text.</summary>
        public void PrintLicense()
        {
            PrintButton_Click(null, null);
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            var text = LicenseText?.Text;
            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() != true)
                    return;

                var paragraph = new Paragraph();
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    paragraph.Inlines.Add(new Run(lines[i]));
                    if (i < lines.Length - 1)
                        paragraph.Inlines.Add(new LineBreak());
                }

                var doc = new FlowDocument(paragraph)
                {
                    PageWidth = printDialog.PrintableAreaWidth,
                    PageHeight = printDialog.PrintableAreaHeight,
                    PagePadding = new Thickness(48),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    Foreground = Brushes.Black
                };

                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                printDialog.PrintDocument(paginator, "X-PHY License Agreement");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not print: {ex.Message}", "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
