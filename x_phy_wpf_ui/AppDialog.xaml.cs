#nullable enable
using System;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace x_phy_wpf_ui
{
    /// <summary>
    /// In-app dialog that replaces Windows MessageBox. Supports OK-only and Yes/No with Information, Warning, or Error icon.
    /// </summary>
    public partial class AppDialog : Window
    {
        private bool _yesClicked;

        public AppDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Show an OK-only dialog. Uses Application.Current.MainWindow as owner when owner is null.
        /// </summary>
        public static void Show(string message, string title = "X-PHY", MessageBoxImage icon = MessageBoxImage.Information)
        {
            Show(Application.Current.MainWindow, message, title, icon);
        }

        /// <summary>
        /// Show an OK-only dialog with explicit owner. Uses blur overlay and keeps dialog within app when owner is MainWindow.
        /// </summary>
        public static void Show(Window? owner, string message, string title = "X-PHY", MessageBoxImage icon = MessageBoxImage.Information)
        {
            var dialog = new AppDialog();
            dialog.Configure(title, message, icon, showYesNo: false);
            owner = owner ?? Application.Current.MainWindow;
            if (owner != null)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                if (owner is MainWindow mw)
                {
                    mw.SetDialogOverlayVisible(true);
                    dialog.Closed += (s, e) => mw.SetDialogOverlayVisible(false);
                }
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            dialog.Show();
            dialog.Activate();
        }

        /// <summary>
        /// Show a Yes/No dialog. Returns true if user clicked Yes, false if No. Uses Application.Current.MainWindow as owner when owner is null.
        /// </summary>
        public static bool? ShowYesNo(string message, string title = "X-PHY")
        {
            return ShowYesNo(Application.Current.MainWindow, message, title);
        }

        /// <summary>
        /// Show a Yes/No dialog with explicit owner. Returns true if user clicked Yes, false if No. Uses blur overlay when owner is MainWindow.
        /// </summary>
        public static bool? ShowYesNo(Window? owner, string message, string title = "X-PHY")
        {
            var dialog = new AppDialog();
            dialog.Configure(title, message, MessageBoxImage.Question, showYesNo: true);
            owner = owner ?? Application.Current.MainWindow;
            if (owner != null)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                if (owner is MainWindow mw)
                {
                    mw.SetDialogOverlayVisible(true);
                    dialog.Closed += (s, e) => mw.SetDialogOverlayVisible(false);
                }
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            dialog.ShowDialog();
            return dialog._yesClicked;
        }

        private void Configure(string title, string message, MessageBoxImage icon, bool showYesNo)
        {
            Title = title;
            TitleText.Text = title;
            MessageText.Text = message;
            IconControl.Kind = MapIcon(icon);
            IconControl.Foreground = GetIconBrush(icon);
            OkButtonPanel.Visibility = showYesNo ? Visibility.Collapsed : Visibility.Visible;
            YesNoButtonPanel.Visibility = showYesNo ? Visibility.Visible : Visibility.Collapsed;
        }

        private static PackIconKind MapIcon(MessageBoxImage icon)
        {
            return icon switch
            {
                MessageBoxImage.Warning => PackIconKind.Alert,
                MessageBoxImage.Error => PackIconKind.AlertCircle,
                MessageBoxImage.Question => PackIconKind.HelpCircle,
                _ => PackIconKind.Information
            };
        }

        private static Brush GetIconBrush(MessageBoxImage icon)
        {
            var key = icon switch
            {
                MessageBoxImage.Warning => "Brush.Warning",
                MessageBoxImage.Error => "Brush.Error",
                MessageBoxImage.Question => "Brush.Info",
                _ => "Brush.Info"
            };
            return (Brush)Application.Current.FindResource(key);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            _yesClicked = true;
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            _yesClicked = false;
            DialogResult = false;
            Close();
        }
    }
}
