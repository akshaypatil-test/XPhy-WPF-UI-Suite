using System;

namespace x_phy_wpf_ui
{
    public class SignInFailedEventArgs : EventArgs
    {
        public string Message { get; }

        public SignInFailedEventArgs(string message)
        {
            Message = message ?? "";
        }
    }
}
