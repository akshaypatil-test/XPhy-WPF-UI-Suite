namespace x_phy_wpf_ui.Services
{
    /// <summary>
    /// Stripe payment service - provides Stripe publishable key for client-side payment form
    /// </summary>
    public static class StripePaymentService
    {
        // Stripe Publishable Key - safe to expose in client-side code (WebView2)
        public const string StripePublishableKey = "pk_live_M3XGQteH57lyHjEUI54vyFDB00jYw28bIB";
    }
}
