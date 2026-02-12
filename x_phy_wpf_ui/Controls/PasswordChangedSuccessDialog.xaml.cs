using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace x_phy_wpf_ui.Controls
{
    public partial class PasswordChangedSuccessDialog : UserControl
    {
        private DispatcherTimer? _timer;
        private int _countdown = 5;

        public PasswordChangedSuccessDialog()
        {
            InitializeComponent();
        }

        public void StartCountdown(Action onComplete)
        {
            _countdown = 5;
            CountdownText.Text = $"Signing Out In {_countdown}...";
            _timer?.Stop();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                _countdown--;
                CountdownText.Text = _countdown > 0 ? $"Signing Out In {_countdown}..." : "Signing out...";
                if (_countdown <= 0)
                {
                    _timer?.Stop();
                    onComplete();
                }
            };
            _timer.Start();
        }

        public void StopCountdown()
        {
            _timer?.Stop();
            _timer = null;
        }
    }
}
