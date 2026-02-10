using System.Windows;
using System.Windows.Controls;

namespace x_phy_wpf_ui.Controls
{
    public partial class StatisticsCards : UserControl
    {
        public static readonly DependencyProperty TotalDetectionsProperty =
            DependencyProperty.Register(nameof(TotalDetections), typeof(string), typeof(StatisticsCards), 
                new PropertyMetadata("0", OnTotalDetectionsChanged));

        public static readonly DependencyProperty TotalDeepfakesProperty =
            DependencyProperty.Register(nameof(TotalDeepfakes), typeof(string), typeof(StatisticsCards), 
                new PropertyMetadata("0", OnTotalDeepfakesChanged));

        public static readonly DependencyProperty TotalAnalysisTimeProperty =
            DependencyProperty.Register(nameof(TotalAnalysisTime), typeof(string), typeof(StatisticsCards), 
                new PropertyMetadata("0h", OnTotalAnalysisTimeChanged));

        public static readonly DependencyProperty LastDetectionProperty =
            DependencyProperty.Register(nameof(LastDetection), typeof(string), typeof(StatisticsCards), 
                new PropertyMetadata("Never", OnLastDetectionChanged));

        public string TotalDetections
        {
            get => (string)GetValue(TotalDetectionsProperty);
            set => SetValue(TotalDetectionsProperty, value);
        }

        public string TotalDeepfakes
        {
            get => (string)GetValue(TotalDeepfakesProperty);
            set => SetValue(TotalDeepfakesProperty, value);
        }

        public string TotalAnalysisTime
        {
            get => (string)GetValue(TotalAnalysisTimeProperty);
            set => SetValue(TotalAnalysisTimeProperty, value);
        }

        public string LastDetection
        {
            get => (string)GetValue(LastDetectionProperty);
            set => SetValue(LastDetectionProperty, value);
        }

        public StatisticsCards()
        {
            InitializeComponent();
        }

        private static void OnTotalDetectionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatisticsCards control && control.TotalDetectionsText != null)
            {
                control.TotalDetectionsText.Text = e.NewValue?.ToString() ?? "0";
            }
        }

        private static void OnTotalDeepfakesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatisticsCards control && control.TotalDeepfakesText != null)
            {
                control.TotalDeepfakesText.Text = e.NewValue?.ToString() ?? "0";
            }
        }

        private static void OnTotalAnalysisTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatisticsCards control && control.TotalAnalysisTimeText != null)
            {
                control.TotalAnalysisTimeText.Text = e.NewValue?.ToString() ?? "0h";
            }
        }

        private static void OnLastDetectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatisticsCards control && control.LastDetectionText != null)
            {
                control.LastDetectionText.Text = e.NewValue?.ToString() ?? "Never";
            }
        }
    }
}
