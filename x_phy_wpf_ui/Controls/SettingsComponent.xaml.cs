using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class SettingsComponent : UserControl
    {
        /// <summary>Settings-page-only background for Update/System Info cards in light theme (very light cyan).</summary>
        private static readonly Brush SettingsCardsLightBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FBFD"));

        public event EventHandler BackClicked;

        public SettingsComponent()
        {
            InitializeComponent();
            Loaded += SettingsComponent_Loaded;
        }

        private bool _updatingAutoStart;
        private readonly SemaphoreSlim _updateCheckGate = new(1, 1);

        private void SettingsComponent_Loaded(object sender, RoutedEventArgs e)
        {
            SyncSettingsReadOnlyLabels();
            UpdateThemeSelection();
            _updatingAutoStart = true;
            try
            {
                AutoStartToggle.IsChecked = StartupHelper.IsAutoStartEnabled();
            }
            finally
            {
                _updatingAutoStart = false;
            }
        }

        /// <summary>Call when the user navigates to Settings (not from <see cref="Loaded"/>, which runs at app startup while this control is in the tree).</summary>
        public void OnNavigatedToSettings()
        {
            SyncSettingsReadOnlyLabels();
        }

        private void SyncSettingsReadOnlyLabels()
        {
            if (AppVersionText != null)
                AppVersionText.Text = ApplicationVersion.GetDisplayVersion();
            if (ReleaseDateText != null)
                ReleaseDateText.Text = ApplicationVersion.GetReleaseDateDisplay();
            if (LastCheckedText != null)
                LastCheckedText.Text = UpdateCheckStateStore.FormatLastCheckedDisplay(UpdateCheckStateStore.LoadLastCheckUtc());
            UpdateCurrentStatusDisplay();
        }

        private void AutoStartToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_updatingAutoStart)
                return;
            try
            {
                StartupHelper.SetAutoStart(AutoStartToggle.IsChecked == true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto start: {ex.Message}");
                AppDialog.Show(Window.GetWindow(this), "Could not change startup setting. " + ex.Message, "Auto Start Application", MessageBoxImage.Warning);
                _updatingAutoStart = true;
                try { AutoStartToggle.IsChecked = StartupHelper.IsAutoStartEnabled(); }
                finally { _updatingAutoStart = false; }
            }
        }

        private void UpdateThemeSelection()
        {
            var currentTheme = ThemeManager.CurrentTheme;
            
            if (currentTheme == ThemeManager.Theme.Light)
            {
                // Light theme is selected - gradient background
                LightThemeCard.Style = (Style)FindResource("ThemeCardSelectedStyle");
                DarkThemeCard.Style = (Style)FindResource("ThemeCardStyle");
                
                // Update text colors
                LightThemeText.Foreground = Brushes.White;
                DarkThemeText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextPrimary");
                
                // Update icon colors
                LightThemeIcon.Fill = Brushes.White;
                DarkThemeIcon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "Brush.TextPrimary");

                // Settings-only: very light cyan background for Update Controls and System Info cards
                if (UpdateControlsCard != null) UpdateControlsCard.Background = SettingsCardsLightBackground;
                if (SystemInfoCard != null) SystemInfoCard.Background = SettingsCardsLightBackground;
            }
            else
            {
                // Dark theme is selected - gradient background
                LightThemeCard.Style = (Style)FindResource("ThemeCardStyle");
                DarkThemeCard.Style = (Style)FindResource("ThemeCardSelectedStyle");
                
                // Update text colors
                LightThemeText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextPrimary");
                DarkThemeText.Foreground = Brushes.White;
                
                // Update icon colors
                LightThemeIcon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "Brush.TextPrimary");
                DarkThemeIcon.Fill = Brushes.White;

                // Settings-only: use theme surface (same as rest of app)
                var surface = (Brush)FindResource("Brush.Surface");
                if (UpdateControlsCard != null) UpdateControlsCard.Background = surface;
                if (SystemInfoCard != null) SystemInfoCard.Background = surface;
            }
        }

        private void LightTheme_Click(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Settings: Light theme clicked");
            ThemeManager.ApplyTheme(ThemeManager.Theme.Light);
            UpdateThemeSelection();
        }

        private void DarkTheme_Click(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Settings: Dark theme clicked");
            ThemeManager.ApplyTheme(ThemeManager.Theme.Dark);
            UpdateThemeSelection();
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            await _updateCheckGate.WaitAsync().ConfigureAwait(true);
            try
            {
                await RunUpdateCheckCoreAsync().ConfigureAwait(true);
            }
            finally
            {
                _updateCheckGate.Release();
            }
        }

        private async Task RunUpdateCheckCoreAsync()
        {
            var owner = Window.GetWindow(this);
            var service = new UpdateCheckService();
            var currentVersion = ApplicationVersion.GetDisplayVersion();

            UpdateCheckResult result;
            try
            {
                result = await service.CheckAsync(currentVersion).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                RecordUpdateCheckFailed();
                AppDialog.Show(owner, "Update check failed: " + ex.Message, "Update Check", MessageBoxImage.Warning);
                return;
            }

            if (!result.Ok)
            {
                RecordUpdateCheckFailed();
                AppDialog.Show(owner, result.ErrorMessage ?? "Update check failed.", "Update Check", MessageBoxImage.Warning);
                return;
            }

            var r = result.Response;
            var updateAvailable = r?.IsUpdateAvailable == true;
            RecordUpdateCheckSucceeded(updateAvailable, r?.LatestVersion);

            if (r == null || !r.IsUpdateAvailable)
            {
                AppDialog.Show(owner, "You are running the latest version!", "Update Check", MessageBoxImage.Information);
                return;
            }

            var msg =
                "A newer version is available : "
                + r.LatestVersion
                + "\nDo you want to download?";

            if (AppDialog.ShowYesNo(owner, msg, "Update available") != true)
                return;

            if (string.IsNullOrWhiteSpace(r.DownloadUrl))
            {
                AppDialog.Show(owner, "No download URL was provided by the server.", "Update Check", MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(
                    new ProcessStartInfo { FileName = r.DownloadUrl.Trim(), UseShellExecute = true }
                );
            }
            catch (Exception ex)
            {
                AppDialog.Show(owner, "Could not open the download link: " + ex.Message, "Update Check", MessageBoxImage.Warning);
            }
        }

        private void RecordUpdateCheckFailed()
        {
            UpdateCheckStateStore.PersistFailedCheck(DateTime.UtcNow);
            RefreshUpdateCheckLabels();
        }

        private void RecordUpdateCheckSucceeded(bool updateAvailable, string latestVersion)
        {
            UpdateCheckStateStore.PersistSuccessfulCheck(DateTime.UtcNow, updateAvailable, latestVersion);
            RefreshUpdateCheckLabels();
        }

        private void RefreshUpdateCheckLabels()
        {
            if (LastCheckedText != null)
                LastCheckedText.Text = UpdateCheckStateStore.FormatLastCheckedDisplay(UpdateCheckStateStore.LoadLastCheckUtc());
            UpdateCurrentStatusDisplay();
        }

        private void UpdateCurrentStatusDisplay()
        {
            if (CurrentStatusText == null || CurrentStatusBadge == null)
                return;
            var current = ApplicationVersion.GetDisplayVersion();
            var upgradePending = UpdateCheckStateStore.IsUpgradePendingVersusCurrent(current);
            CurrentStatusText.Text = upgradePending ? "Upgrade available" : "Up to date";
            CurrentStatusBadge.SetResourceReference(
                Border.BackgroundProperty,
                upgradePending ? "Brush.Warning" : "Brush.Success");
        }
    }
}
