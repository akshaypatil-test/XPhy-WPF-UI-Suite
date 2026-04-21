using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace x_phy_wpf_ui.Controls
{
    public partial class SupportComponent : UserControl
    {
        private const string SupportUrl = "https://x-phy.com/products/endpoint-security/deepfake-detector/support/";
        private const string TicketUrl = "https://support.x-phy.com/support/tickets/new";
        private const string UserGuideVideoUrl = "https://www.youtube.com/watch?v=Kc_y__h5r0U";
        private static readonly string ResourcesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
        private const string UserGuidePdfFileName = "UserGuide.pdf";
        private const string FaqPdfFileName = "FAQ.pdf";

        public SupportComponent()
        {
            InitializeComponent();
        }

        private void OpenTicket_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(TicketUrl);
        }

        private void HelpSupport_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(SupportUrl);
        }

        private void OpenUrl(string url)
        {
            try
            {
                var psi = new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                AppDialog.Show(Window.GetWindow(this), $"Could not open link: {ex.Message}", "Error", MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Opens a local file with the shell default handler (for PDFs this is often the default browser such as Edge/Chrome when set as the PDF app).
        /// </summary>
        private void OpenLocalFileInDefaultHandler(string fullPath)
        {
            try
            {
                var psi = new ProcessStartInfo(fullPath)
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                AppDialog.Show(
                    Window.GetWindow(this),
                    $"The file was saved to your Downloads folder but could not be opened automatically.\n\n{fullPath}\n\n{ex.Message}",
                    "Could not open file",
                    MessageBoxImage.Warning);
            }
        }

        private void UserGuidePdf_Click(object sender, RoutedEventArgs e)
        {
            DownloadResourceFile(
                UserGuidePdfFileName,
                "Installation Setup & User Guide",
                "Installation Setup & User Guide.pdf");
        }

        private void FaqPdf_Click(object sender, RoutedEventArgs e)
        {
            DownloadResourceFile(
                FaqPdfFileName,
                "FAQ / Knowledge Base",
                "F.A.Q Knowledgebase (version 2.0.0).pdf");
        }

        private void UserGuideVideo_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(UserGuideVideoUrl);
        }

        private void DownloadResourceFile(string fileName, string displayName, string downloadFileName)
        {
            var sourcePath = Path.Combine(ResourcesFolder, fileName);
            if (!File.Exists(sourcePath))
            {
                AppDialog.Show(Window.GetWindow(this),
                    $"The file '{displayName}' was not found. Please ensure Resources are installed.",
                    "File not found",
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                var downloadsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads");
                Directory.CreateDirectory(downloadsFolder);

                var destinationPath = Path.Combine(downloadsFolder, downloadFileName);
                File.Copy(sourcePath, destinationPath, overwrite: true);
                OpenLocalFileInDefaultHandler(destinationPath);
            }
            catch (Exception ex)
            {
                AppDialog.Show(Window.GetWindow(this), $"Could not download file: {ex.Message}", "Error", MessageBoxImage.Warning);
            }
        }
    }
}
