namespace x_phy_wpf_ui.Services
{
    /// <summary>
    /// Stripe payment service - provides Stripe publishable key for client-side payment form
    /// </summary>
    public static class StripePaymentService
    {
        // Stripe Publishable Key - safe to expose in client-side code (WebView2)
        public const string StripePublishableKey = "pk_test_vqRv78JyvVS5GIsNafg8KNZF00TaKyxi0w";
    }
}
