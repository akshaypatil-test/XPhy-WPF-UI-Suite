using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace x_phy_wpf_ui.Controls
{
    public partial class WelcomeComponent : UserControl
    {
        public event EventHandler NavigateToLaunch;

        private DispatcherTimer timer;

        public WelcomeComponent()
        {
            InitializeComponent();
            
            // Create timer for 3 seconds
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(7);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            timer.Stop();
            NavigateToLaunch?.Invoke(this, EventArgs.Empty);
        }
    }
}
