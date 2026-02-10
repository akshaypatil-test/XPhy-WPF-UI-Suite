using System;
using System.Windows;
using System.Windows.Controls;

namespace x_phy_wpf_ui.Controls
{
    public partial class TopNavigationBar : UserControl
    {
        public event EventHandler<string> NavigationClicked;
        public event EventHandler AddCorpUserClicked;

        public static readonly DependencyProperty SelectedPageProperty =
            DependencyProperty.Register(
                nameof(SelectedPage),
                typeof(string),
                typeof(TopNavigationBar),
                new PropertyMetadata("Home", OnSelectedPageChanged));

        public string SelectedPage
        {
            get => (string)GetValue(SelectedPageProperty);
            set => SetValue(SelectedPageProperty, value);
        }

        public static readonly DependencyProperty IsAdminProperty =
            DependencyProperty.Register(
                nameof(IsAdmin),
                typeof(bool),
                typeof(TopNavigationBar),
                new PropertyMetadata(false, OnIsAdminChanged));

        public bool IsAdmin
        {
            get => (bool)GetValue(IsAdminProperty);
            set => SetValue(IsAdminProperty, value);
        }

        private static void OnIsAdminChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TopNavigationBar bar && bar.AddCorpUserButton != null)
                bar.AddCorpUserButton.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public TopNavigationBar()
        {
            InitializeComponent();
            UpdateSelection("Home");
            if (AddCorpUserButton != null)
                AddCorpUserButton.Visibility = IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void OnSelectedPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TopNavigationBar navBar)
            {
                navBar.UpdateSelection(e.NewValue as string ?? "Home");
            }
        }

        private void UpdateSelection(string selectedPage)
        {
            // Reset all buttons to default style
            HomeNavButton.Style = (Style)FindResource("TopNavButtonStyle");
            ResultsNavButton.Style = (Style)FindResource("TopNavButtonStyle");
            ProfileNavButton.Style = (Style)FindResource("TopNavButtonStyle");
            SettingsNavButton.Style = (Style)FindResource("TopNavButtonStyle");
            if (AddCorpUserButton != null)
                AddCorpUserButton.Style = (Style)FindResource("TopNavButtonStyle");

            // Set selected button style
            Button selectedButton = selectedPage switch
            {
                "Home" => HomeNavButton,
                "Results" => ResultsNavButton,
                "Profile" => ProfileNavButton,
                "Settings" => SettingsNavButton,
                "CorpUser" => AddCorpUserButton,
                _ => HomeNavButton
            };

            if (selectedButton != null)
                selectedButton.Style = (Style)FindResource("SelectedTopNavButtonStyle");
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            SelectedPage = "Home";
            NavigationClicked?.Invoke(this, "Home");
        }

        private void Results_Click(object sender, RoutedEventArgs e)
        {
            SelectedPage = "Results";
            NavigationClicked?.Invoke(this, "Results");
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            SelectedPage = "Profile";
            NavigationClicked?.Invoke(this, "Profile");
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            SelectedPage = "Settings";
            NavigationClicked?.Invoke(this, "Settings");
        }

        private void AddCorpUser_Click(object sender, RoutedEventArgs e)
        {
            SelectedPage = "CorpUser";
            AddCorpUserClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
