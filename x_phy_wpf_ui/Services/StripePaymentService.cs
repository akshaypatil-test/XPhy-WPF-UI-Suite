namespace x_phy_wpf_ui.Services
{
    /// <summary>
    /// Stripe payment service - provides Stripe publishable key for client-side payment form
    /// </summary>
    public static class StripePaymentService
    {
        // Stripe Publishable Key - safe to expose in client-side code (WebView2)
        public const string StripePublishableKey = "pk_test_51SryZEDJn21okKgxCzb5rO2rq6hQRLhGofquKVYWed6Mh0x9QRYMCHPlnY93jE5SBTmsRngHKi0snYuP7oiHVV1s00udVr2Hme";
    }
}
