#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;

namespace x_phy_wpf_ui.Controls
{
    public partial class DetectionSelection : UserControl
    {
        public event EventHandler<DetectionSelectionEventArgs>? StartDetectionRequested;
        public event EventHandler? CancelRequested;

        private ProcessDetectionService? _processDetectionService;
        private List<DetectedProcess> _detectedProcesses = new List<DetectedProcess>();
        private DetectedProcess? _selectedProcess;
        private DetectionSource? _selectedSource;
        private bool _isAudioMode = false;
        private bool _isLiveCallMode = false;

        public DetectionSelection()
        {
            InitializeComponent();
            this.Loaded += DetectionSelection_Loaded;
            this.IsVisibleChanged += DetectionSelection_IsVisibleChanged;
        }

        private void DetectionSelection_Loaded(object sender, RoutedEventArgs e)
        {
            // Load processes when component is first loaded
            RefreshProcesses();
        }

        private void DetectionSelection_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible && e.NewValue is bool isVisible && isVisible)
            {
                RefreshProcesses();
            }
        }

        public void RefreshProcesses()
        {
            try
            {
                if (_processDetectionService == null)
                {
                    _processDetectionService = new ProcessDetectionService();
                }
                
                // Reset selection state
                _selectedProcess = null;
                _selectedSource = null;
                _isAudioMode = false;
                _isLiveCallMode = false;
                StartDetectionButton.IsEnabled = false;
                
                // Clear previous UI state
                ProcessesPanel.Children.Clear();
                SourceOptionsGrid.Children.Clear();
                
                // Reload processes
                LoadDetectedProcesses();
            }
            catch (Exception ex)
            {
                // Log error but don't crash - show message in UI
                System.Diagnostics.Debug.WriteLine($"Error refreshing DetectionSelection: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (StatusText != null)
                {
                    StatusText.Text = $"Error refreshing: {ex.Message}";
                    StatusText.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
        }

        private void LoadDetectedProcesses()
        {
            try
            {
                if (_processDetectionService == null)
                {
                    _processDetectionService = new ProcessDetectionService();
                }
                
                _detectedProcesses = _processDetectionService.DetectRelevantProcesses();
                PopulateDetectedProcesses();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error detecting processes: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                if (StatusText != null)
                {
                    StatusText.Text = $"Error detecting processes: {ex.Message}";
                    StatusText.Foreground = new SolidColorBrush(Colors.Red);
                }
                
                // Show error message in processes panel
                if (ProcessesPanel != null)
                {
                    ProcessesPanel.Children.Clear();
                    var errorText = new TextBlock
                    {
                        Text = $"Error detecting processes: {ex.Message}\n\nPlease try again later.",
                        Foreground = new SolidColorBrush(Colors.Red),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(20),
                        FontSize = 14
                    };
                    ProcessesPanel.Children.Add(errorText);
                }
            }
        }

        private void PopulateDetectedProcesses()
        {
            ProcessesPanel.Children.Clear();

            if (_detectedProcesses.Count == 0)
            {
                var noProcessesText = new TextBlock
                {
                    Text = "No relevant processes detected.\n\nPlease start a supported app (e.g. Zoom, Teams, Webex, Slack, Discord, VLC, OBS, or open YouTube in Chrome/Edge/Firefox). Only the top 3 detected apps are shown.",
                    Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 16, 0, 0),
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                ProcessesPanel.Children.Add(noProcessesText);
                return;
            }

            for (int i = 0; i < _detectedProcesses.Count; i++)
            {
                var process = _detectedProcesses[i];
                var button = new Button
                {
                    Content = CreateProcessButtonContent(process),
                    Style = (Style)FindResource(i == 0 ? "SelectedProcessButtonStyle" : "ProcessButtonStyle"),
                    Tag = process,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                button.Click += ProcessButton_Click;
                ProcessesPanel.Children.Add(button);
            }
            // Select first source by default and show its input type options
            if (_detectedProcesses.Count > 0)
            {
                _selectedProcess = _detectedProcesses[0];
                ShowSourceOptions(_detectedProcesses[0]);
            }
        }

        private StackPanel CreateProcessButtonContent(DetectedProcess process)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // Camera icon (pink color to match Figma)
            var iconText = new TextBlock
            {
                Text = "📹", // Camera icon for all processes
                FontSize = 16,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 21, 107)) // Pink color
            };
            
            // Process name (inherits Foreground from button: dark when unselected, white when selected)
            var processNameText = new TextBlock
            {
                Text = process.DisplayName,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            processNameText.SetBinding(TextBlock.ForegroundProperty,
                new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1) });
            
            panel.Children.Add(iconText);
            panel.Children.Add(processNameText);
            
            return panel;
        }

        private string GetProcessIcon(string processType)
        {
            // Use camera icon for all processes to match Figma design
            return "📹";
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DetectedProcess process)
            {
                // Reset all process buttons
                foreach (var child in ProcessesPanel.Children)
                {
                    if (child is Button btn)
                    {
                        btn.Style = (Style)FindResource("ProcessButtonStyle");
                    }
                }
                
                // Highlight selected button
                button.Style = (Style)FindResource("SelectedProcessButtonStyle");
                _selectedProcess = process;
                
                // Show source options based on process type
                ShowSourceOptions(process);
            }
        }

        private void ShowSourceOptions(DetectedProcess process)
        {
            SourceOptionsGrid.Children.Clear();
            _selectedSource = null;
            StartDetectionButton.IsEnabled = false;
            
            if (process.ProcessType == "VideoCalling")
            {
                var conferenceVideoButton = new Button
                {
                    Content = CreateSourceButtonContent("Conference Video", "📹"),
                    Style = (Style)FindResource("SelectedSourceButtonStyle"),
                    Tag = DetectionSource.ZoomConferenceVideo
                };
                conferenceVideoButton.Click += SourceButton_Click;
                Grid.SetColumn(conferenceVideoButton, 0);
                SourceOptionsGrid.Children.Add(conferenceVideoButton);
                
                var conferenceAudioButton = new Button
                {
                    Content = CreateSourceButtonContent("Conference Audio", "🎤"),
                    Style = (Style)FindResource("SourceButtonStyle"),
                    Tag = DetectionSource.ZoomConferenceAudio
                };
                conferenceAudioButton.Click += SourceButton_Click;
                Grid.SetColumn(conferenceAudioButton, 2);
                SourceOptionsGrid.Children.Add(conferenceAudioButton);
                
                _selectedSource = DetectionSource.ZoomConferenceVideo;
                _isAudioMode = false;
                _isLiveCallMode = true;
                StartDetectionButton.IsEnabled = true;
                StatusText.Text = "Ready to start detection";
            }
            else if (process.ProcessType == "MediaPlayer" || process.ProcessType == "Streaming" || process.ProcessType == "Browser")
            {
                var videoSource = process.ProcessType == "Browser" ? DetectionSource.YouTubeWebStreamVideo : DetectionSource.VLCWebStreamVideo;
                var webVideoButton = new Button
                {
                    Content = CreateSourceButtonContent("Web Stream Video", "📹"),
                    Style = (Style)FindResource("SelectedSourceButtonStyle"),
                    Tag = videoSource
                };
                webVideoButton.Click += SourceButton_Click;
                Grid.SetColumn(webVideoButton, 0);
                SourceOptionsGrid.Children.Add(webVideoButton);
                
                var webAudioButton = new Button
                {
                    Content = CreateSourceButtonContent("Web Stream Audio", "🎤"),
                    Style = (Style)FindResource("SourceButtonStyle"),
                    Tag = (process.ProcessType == "Browser") ? DetectionSource.YouTubeWebStreamAudio : DetectionSource.VLCWebStreamAudio
                };
                webAudioButton.Click += SourceButton_Click;
                Grid.SetColumn(webAudioButton, 2);
                SourceOptionsGrid.Children.Add(webAudioButton);
                
                _selectedSource = videoSource;
                _isAudioMode = false;
                _isLiveCallMode = false;
                StartDetectionButton.IsEnabled = true;
                StatusText.Text = "Ready to start detection";
            }
        }

        private StackPanel CreateSourceButtonContent(string text, string icon)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // Camera icon (pink color to match Figma)
            var iconText = new TextBlock
            {
                Text = icon, // Use the passed icon parameter
                FontSize = 16,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 21, 107)) // Pink color
            };
            
            // Text inherits Foreground from button: dark when unselected, white when selected
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            textBlock.SetBinding(TextBlock.ForegroundProperty,
                new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Button), 1) });
            
            panel.Children.Add(iconText);
            panel.Children.Add(textBlock);
            
            return panel;
        }

        private void SourceButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DetectionSource source)
            {
                // Reset all source buttons
                foreach (var child in SourceOptionsGrid.Children)
                {
                    if (child is Button btn)
                    {
                        btn.Style = (Style)FindResource("SourceButtonStyle");
                    }
                }
                
                // Highlight selected button
                button.Style = (Style)FindResource("SelectedSourceButtonStyle");
                _selectedSource = source;
                
                // Determine mode based on source
                _isAudioMode = source == DetectionSource.ZoomConferenceAudio || 
                              source == DetectionSource.VLCWebStreamAudio || 
                              source == DetectionSource.YouTubeWebStreamAudio;
                _isLiveCallMode = source == DetectionSource.ZoomConferenceVideo || 
                                 source == DetectionSource.ZoomConferenceAudio;
                
                // Enable start button
                StartDetectionButton.IsEnabled = true;
                StatusText.Text = "Ready to start detection";
            }
        }

        private void StartDetection_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProcess != null && _selectedSource.HasValue)
            {
                StartDetectionRequested?.Invoke(this, new DetectionSelectionEventArgs
                {
                    SelectedProcess = _selectedProcess,
                    SelectedSource = _selectedSource.Value,
                    IsLiveCallMode = _isLiveCallMode,
                    IsAudioMode = _isAudioMode
                });
            }
            else
            {
                MessageBox.Show("Please select a process and source before starting detection.", 
                    "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Trigger cancel event to go back to previous component
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public class DetectionSelectionEventArgs : EventArgs
    {
        public DetectedProcess? SelectedProcess { get; set; }
        public DetectionSource? SelectedSource { get; set; }
        public bool IsLiveCallMode { get; set; }
        public bool IsAudioMode { get; set; }
    }
}
