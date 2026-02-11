using System;

namespace x_phy_wpf_ui
{
    /// <summary>Thrown when the refresh token is invalid or expired; app should redirect to sign-in.</summary>
    public class SessionExpiredException : Exception
    {
        public SessionExpiredException()
            : base("Your session has expired. Please sign in again.")
        {
        }

        public SessionExpiredException(string message)
            : base(message)
        {
        }
    }
}
