namespace x_phy_wpf_ui.Services
{
    /// <summary>
    /// Stripe payment service - provides Stripe publishable key for client-side payment form
    /// </summary>
    public static class StripePaymentService
    {
        // Stripe Publishable Key - safe to expose in client-side code (WebView2)
        public const string StripePublishableKey = "pk_live_51TTCh4AFEUNJ0Fk1XULA4NxZoqgkcKMtqF4fLg49kO5H5eaKgtU21x2AnhJsOUBG6ON7InyzBrvusmjXIQG7Yz8u00PfA1mipb";
    }
}
