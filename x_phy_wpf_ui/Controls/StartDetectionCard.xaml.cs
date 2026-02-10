using System;
using System.Windows;
using System.Windows.Controls;

namespace x_phy_wpf_ui.Controls
{
    public partial class StartDetectionCard : UserControl
    {
        public static readonly DependencyProperty StatusTextProperty =
            DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(StartDetectionCard), 
                new PropertyMetadata("Ready to start detection", OnStatusTextChanged));

        public string StatusText
        {
            get => (string)GetValue(StatusTextProperty);
            set => SetValue(StatusTextProperty, value);
        }

        public event EventHandler<RoutedEventArgs> StartDetectionClicked;

        public StartDetectionCard()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartDetectionClicked?.Invoke(this, e);
        }

        private static void OnStatusTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StartDetectionCard control && control.DetectionStatusText != null)
            {
                control.DetectionStatusText.Text = e.NewValue?.ToString() ?? "Ready to start detection";
            }
        }
    }
}
