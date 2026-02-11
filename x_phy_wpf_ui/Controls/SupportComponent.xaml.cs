using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
namespace x_phy_wpf_ui.Controls
{
    public partial class SupportComponent : UserControl
    {
        public event EventHandler? BackRequested;

        private const string SupportUrl = "https://support.x-phy.com/support/home";
        private const string TicketUrl = "https://support.x-phy.com/support/tickets/new";

        private static readonly string ResourcesFolder = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Resources"
        );
        private const string UserGuidePdfFileName = "UserGuide.pdf";
        private const string FaqPdfFileName = "FAQ.pdf";
        private const string UserGuideVideoFileName = "UserGuideVideo.mp4";

        public SupportComponent()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OpenTicket_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(TicketUrl);
        }

        private void HelpSupport_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(SupportUrl);
        }

        private static void OpenUrl(string url)
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
                MessageBox.Show($"Could not open link: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UserGuidePdf_Click(object sender, RoutedEventArgs e)
        {
            OpenOrDownloadResource(UserGuidePdfFileName, "Installation & User Guide");
        }

        private void FaqPdf_Click(object sender, RoutedEventArgs e)
        {
            OpenOrDownloadResource(FaqPdfFileName, "FAQ / Knowledge Base");
        }

        private void UserGuideVideo_Click(object sender, RoutedEventArgs e)
        {
            OpenOrDownloadResource(UserGuideVideoFileName, "User Guide Video");
        }

        private static void OpenOrDownloadResource(string fileName, string displayName)
        {
            var path = Path.Combine(ResourcesFolder, fileName);
            if (!File.Exists(path))
            {
                MessageBox.Show(
                    $"The file '{displayName}' was not found. Please ensure Resources are installed.",
                    "File not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }
            try
            {
                var psi = new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
