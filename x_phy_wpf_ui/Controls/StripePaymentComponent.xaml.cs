using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using x_phy_wpf_ui.Models;
using x_phy_wpf_ui.Services;
using Newtonsoft.Json;
using static x_phy_wpf_ui.Services.StripePaymentService;
using static x_phy_wpf_ui.Services.ThemeManager;

namespace x_phy_wpf_ui.Controls
{
    public partial class StripePaymentComponent : UserControl
    {
        public event EventHandler<PaymentSuccessEventArgs> PaymentSuccess;
        public event EventHandler BackRequested;

        private readonly LicensePlan _plan;
        private readonly LicensePurchaseService _purchaseService;
        private string _clientSecret;
        private string _paymentIntentId;

        public StripePaymentComponent(LicensePlan plan)
        {
            InitializeComponent();
            _plan = plan;
            _purchaseService = new LicensePurchaseService();


            // Use a writable user data folder to avoid E_ACCESSDENIED when app is installed via MSI (e.g. under Program Files).
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "X-PHY", "X-PHY Deepfake Detector", "WebView2");
            try { Directory.CreateDirectory(userDataFolder); } catch { /* use path anyway */ }
            StripeWebView.CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = userDataFolder
            };

            Loaded += StripePaymentComponent_Loaded;
        }

        private async void StripePaymentComponent_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeWebView();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to initialize payment form: {ex.Message}");
            }
        }

        private async Task InitializeWebView()
        {
            try
            {
                // Ensure WebView2 runtime is initialized
                await StripeWebView.EnsureCoreWebView2Async(null);

                // Transparent background so HTML glass effect shows through
                try { StripeWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent; } catch { }

                // Enable script execution and web messaging
                StripeWebView.CoreWebView2.Settings.IsScriptEnabled = true;
                StripeWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                StripeWebView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                // Enable DevTools for debugging (TEMPORARY - remove in production)
                // StripeWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;

                // Register message handler for communication from JavaScript
                StripeWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // Handle navigation completed to check for errors
                StripeWebView.CoreWebView2.NavigationCompleted += (s, args) =>
                {
                    if (!args.IsSuccess)
                    {
                        Dispatcher.Invoke(() => ShowError($"Failed to load payment form. Error: {args.WebErrorStatus}"));
                    }
                    else
                    {
                        // Open DevTools to see console errors (commented out for now)
                        // Dispatcher.Invoke(() =>
                        // {
                        //     try
                        //     {
                        //         StripeWebView.CoreWebView2.OpenDevToolsWindow();
                        //     }
                        //     catch { }
                        // });
                    }
                };

                // Initiate purchase with backend API
                StatusText.Text = "Creating secure payment session...";
                var purchaseResponse = await _purchaseService.InitiatePurchaseAsync(_plan.PlanId);

                if (purchaseResponse == null)
                {
                    ShowError("Failed to initiate purchase. Please try again.");
                    return;
                }

                _clientSecret = purchaseResponse.ClientSecret;
                _paymentIntentId = purchaseResponse.PaymentIntentId;

                // Generate and load payment form
                StatusText.Text = "Loading payment form...";
                var html = GenerateStripeHtml(_clientSecret, _plan.Price);

                // Use writable app data folder for HTML (avoids E_ACCESSDENIED when temp is restricted after MSI install).
                string paymentFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "X-PHY", "X-PHY Deepfake Detector", "WebView2", "Payment");
                Directory.CreateDirectory(paymentFolder);
                string htmlPath = Path.Combine(paymentFolder, $"stripe_payment_{Guid.NewGuid()}.html");
                File.WriteAllText(htmlPath, html);

                StripeWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "payment.local",
                    paymentFolder,
                    CoreWebView2HostResourceAccessKind.Allow);

                string fileName = Path.GetFileName(htmlPath);
                StripeWebView.CoreWebView2.Navigate($"https://payment.local/{fileName}");
                
                StatusText.Text = "";
            }
            catch (Exception ex)
            {
                ShowError($"Error initializing payment: {ex.Message}");
            }
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = e.TryGetWebMessageAsString();
                System.Diagnostics.Debug.WriteLine($"Received message from JavaScript: {message}");
                
                if (string.IsNullOrEmpty(message))
                {
                    System.Diagnostics.Debug.WriteLine("Received empty message");
                    return;
                }
                
                var data = JsonConvert.DeserializeObject<PaymentMessage>(message);
                
                if (data == null)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to deserialize message");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"Message type: {data.Type}");

                Dispatcher.Invoke(async () =>
                {
                    switch (data.Type)
                    {
                        case "payment_processing":
                            StatusText.Text = "Processing payment...";
                            StatusText.Foreground = (SolidColorBrush)FindResource("Brush.TextSecondary");
                            ErrorMessageText.Visibility = Visibility.Collapsed;
                            break;

                        case "payment_success":
                            System.Diagnostics.Debug.WriteLine("Handling payment success");
                            await HandlePaymentSuccess();
                            break;

                        case "payment_error":
                            ShowError($"Payment failed: {data.Message}");
                            break;
                            
                        default:
                            System.Diagnostics.Debug.WriteLine($"Unknown message type: {data.Type}");
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in WebMessageReceived: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                Dispatcher.Invoke(() => ShowError($"Error processing payment: {ex.Message}"));
            }
        }

        private async Task HandlePaymentSuccess()
        {
            try
            {
                StatusText.Text = "Confirming purchase...";

                var confirmResponse = await _purchaseService.ConfirmPurchaseAsync(_paymentIntentId);

                if (confirmResponse != null)
                {
                    if (confirmResponse.License != null)
                    {
                        var tokenStorage = new TokenStorage();
                        var current = tokenStorage.GetTokens();
                        var userInfo = confirmResponse.User ?? current?.UserInfo;
                        if (userInfo != null && !string.IsNullOrEmpty(confirmResponse.License.Status))
                            userInfo = new UserInfo { Id = userInfo.Id, Username = userInfo.Username, LicenseStatus = confirmResponse.License.Status, TrialEndsAt = userInfo.TrialEndsAt, UserType = userInfo.UserType };
                        tokenStorage.UpdateUserAndLicense(userInfo, confirmResponse.License);
                        if (!string.IsNullOrWhiteSpace(confirmResponse.License.Key))
                            MainWindow.WriteLicenseKeyToExeConfig(confirmResponse.License.Key);
                    }

                    PaymentSuccess?.Invoke(this, new PaymentSuccessEventArgs(
                        _plan.Name,
                        _plan.DurationDays,
                        _plan.Price,
                        _paymentIntentId
                    ));
                }
                else
                {
                    ShowError("Failed to confirm purchase. Please contact support.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error confirming purchase: {ex.Message}");
            }
        }

        private string GenerateStripeHtml(string clientSecret, decimal amount)
        {
            bool isLight = CurrentTheme == Theme.Light;
            string bodyClass = isLight ? "light" : "dark";
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>Stripe Payment</title>
    <script src=""https://js.stripe.com/v3/""></script>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: 'Segoe UI', Arial, sans-serif;
            background: transparent;
            padding: 24px 20px 80px 20px;
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: flex-start;
        }}
        body.dark {{ color: #ffffff; }}
        body.light {{ color: #1A202C; }}
        .form-container {{
            width: 100%;
            max-width: 380px;
            margin: 0 auto;
        }}
        .section-label {{
            display: block;
            font-size: 12px;
            font-weight: 600;
            margin-bottom: 8px;
        }}
        body.dark .section-label {{ color: #ffffff; }}
        body.light .section-label {{ color: #1A202C; }}
        .card-info-block {{
            border-radius: 10px;
            overflow: hidden;
            margin-bottom: 0;
        }}
        body.dark .card-info-block {{ border: 1px solid rgba(255,255,255,0.4); background: rgba(0,0,0,0.35); }}
        body.light .card-info-block {{ border: 1px solid #E0E0E0; background: #FFFFFF; }}
        .card-info-block .element-wrap {{
            border: none;
            margin: 0;
            border-radius: 0;
            padding: 10px 12px;
            overflow: hidden;
        }}
        body.dark .card-info-block .element-wrap {{ background: transparent; border-bottom: 1px solid rgba(255,255,255,0.25); }}
        body.light .card-info-block .element-wrap {{ background: transparent; border-bottom: 1px solid #E0E0E0; }}
        .card-info-block .row-2 .element-wrap {{ border-bottom: none; }}
        .card-info-block .row-2 {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 0;
            margin-bottom: 0;
        }}
        body.dark .card-info-block .row-2 > div:first-child .element-wrap {{ border-right: 1px solid rgba(255,255,255,0.25); }}
        body.light .card-info-block .row-2 > div:first-child .element-wrap {{ border-right: 1px solid #E0E0E0; }}
        .element-wrap {{
            border-radius: 10px;
            padding: 10px 12px;
            margin-bottom: 12px;
            overflow: hidden;
        }}
        body.dark .element-wrap {{ border: 1px solid rgba(255,255,255,0.4); background: rgba(0,0,0,0.35); }}
        body.light .element-wrap {{ border: 1px solid #E0E0E0; background: #FFFFFF; }}
        #cardholder-name {{
            width: 100%;
            padding: 10px 12px;
            font-size: 14px;
            font-family: 'Segoe UI', Arial, sans-serif;
            border-radius: 10px;
            margin-bottom: 16px;
        }}
        body.dark #cardholder-name {{ color: #ffffff; background: rgba(0,0,0,0.35); border: 1px solid rgba(255,255,255,0.4); }}
        body.light #cardholder-name {{ color: #1A202C; background: #FFFFFF; border: 1px solid #E0E0E0; }}
        body.dark #cardholder-name::placeholder {{ color: #9ca3af; }}
        body.light #cardholder-name::placeholder {{ color: #718096; }}
        #submit-button {{
            background: #E2156B;
            color: white;
            border: none;
            border-radius: 10px;
            padding: 12px 20px;
            font-size: 14px;
            font-weight: 600;
            width: 100%;
            cursor: pointer;
            transition: background 0.2s;
        }}
        #submit-button:hover:not(:disabled) {{
            background: #F0458A;
        }}
        #submit-button:disabled {{
            opacity: 0.6;
            cursor: not-allowed;
        }}
        #error-message {{
            font-size: 11px;
            margin-top: 6px;
            padding: 4px 0;
            background: transparent;
            border: none;
            border-radius: 0;
            display: none;
            line-height: 1.3;
        }}
        body.dark #error-message {{ color: #fca5a5; }}
        body.light #error-message {{ color: #B91C1C; }}
        .spinner {{
            display: inline-block;
            width: 14px;
            height: 14px;
            border: 2px solid rgba(255,255,255,.3);
            border-radius: 50%;
            border-top-color: #fff;
            animation: spin 0.8s ease-in-out infinite;
            margin-left: 6px;
        }}
        body.light .spinner {{ border-color: rgba(0,0,0,.15); border-top-color: #1A202C; }}
        @keyframes spin {{
            to {{ transform: rotate(360deg); }}
        }}
        .field-group {{
            margin-bottom: 16px;
        }}
    </style>
</head>
<body class=""{bodyClass}"">
    <div class=""form-container"">
    <form id=""payment-form"">
        <div class=""field-group"">
            <span class=""section-label"">Card information</span>
            <div class=""card-info-block"">
                <div id=""card-number-element"" class=""element-wrap""></div>
                <div class=""row-2"">
                    <div>
                        <div id=""card-expiry-element"" class=""element-wrap""></div>
                    </div>
                    <div>
                        <div id=""card-cvc-element"" class=""element-wrap""></div>
                    </div>
                </div>
            </div>
        </div>
        
        <div class=""field-group"">
            <span class=""section-label"">Cardholder name</span>
            <input type=""text"" id=""cardholder-name"" name=""cardholderName"" placeholder=""Full name on card"" autocomplete=""cc-name"" />
        </div>
        
        <button id=""submit-button"" type=""submit"">
            <span id=""button-text"">Pay Securely</span>
            <span id=""button-spinner"" class=""spinner"" style=""display:none;""></span>
        </button>
        <div id=""error-message""></div>
    </form>
    </div>

    <script>
        (function() {{
            try {{
                console.log('Starting Stripe initialization...');
                console.log('Stripe key:', '{StripePaymentService.StripePublishableKey}'.substring(0, 20) + '...');
                console.log('Client secret:', '{clientSecret}'.substring(0, 20) + '...');
                
                if (typeof Stripe === 'undefined') {{
                    throw new Error('Stripe.js failed to load');
                }}
                
                const stripe = Stripe('{StripePaymentService.StripePublishableKey}');
                console.log('Stripe object created');
                
                const elements = stripe.elements();
                const isLight = {isLight.ToString().ToLower()};
                const elementStyle = isLight ? {{
                    base: {{
                        fontSize: '14px',
                        fontFamily: 'Segoe UI, Arial, sans-serif',
                        color: '#1A202C',
                        '::placeholder': {{ color: '#718096' }},
                        iconColor: '#718096'
                    }},
                    invalid: {{ 
                        color: '#B91C1C',
                        iconColor: '#B91C1C'
                    }}
                }} : {{
                    base: {{
                        fontSize: '14px',
                        fontFamily: 'Segoe UI, Arial, sans-serif',
                        color: '#ffffff',
                        '::placeholder': {{ color: '#9ca3af' }},
                        iconColor: '#9ca3af'
                    }},
                    invalid: {{ 
                        color: '#fca5a5',
                        iconColor: '#fca5a5'
                    }}
                }};
                
                const cardNumberElement = elements.create('cardNumber', {{ style: elementStyle, showIcon: true }});
                const cardExpiryElement = elements.create('cardExpiry', {{ style: elementStyle }});
                const cardCvcElement = elements.create('cardCvc', {{ style: elementStyle }});
                
                cardNumberElement.mount('#card-number-element');
                cardExpiryElement.mount('#card-expiry-element');
                cardCvcElement.mount('#card-cvc-element');
                console.log('Card elements mounted');
                
                const form = document.getElementById('payment-form');
                const submitButton = document.getElementById('submit-button');
                const buttonText = document.getElementById('button-text');
                const buttonSpinner = document.getElementById('button-spinner');
                const errorMessage = document.getElementById('error-message');
                const cardholderInput = document.getElementById('cardholder-name');
                
                const displayError = function(event) {{
                    if (event.error) {{
                        errorMessage.textContent = event.error.message;
                        errorMessage.style.display = 'block';
                    }} else {{
                        errorMessage.style.display = 'none';
                    }}
                }};
                
                cardNumberElement.on('change', displayError);
                cardExpiryElement.on('change', displayError);
                cardCvcElement.on('change', displayError);
                
                form.addEventListener('submit', async function(event) {{
                    event.preventDefault();
                    
                    const cardholderName = (cardholderInput && cardholderInput.value) ? cardholderInput.value.trim() : '';
                    if (!cardholderName) {{
                        errorMessage.textContent = 'Please enter the cardholder name.';
                        errorMessage.style.display = 'block';
                        return;
                    }}
                    
                    submitButton.disabled = true;
                    buttonText.textContent = 'Processing...';
                    buttonSpinner.style.display = 'inline-block';
                    errorMessage.style.display = 'none';
                    
                    try {{
                        try {{
                            window.chrome.webview.postMessage(JSON.stringify({{ type: 'payment_processing' }}));
                        }} catch (postError) {{
                            console.error('Failed to send processing message:', postError);
                        }}
                        
                        console.log('Confirming card payment...');
                        const {{error, paymentIntent}} = await stripe.confirmCardPayment(
                            '{clientSecret}',
                            {{
                                payment_method: {{ 
                                    card: cardNumberElement,
                                    billing_details: {{ name: cardholderName }}
                                }}
                            }}
                        );
                        
                        if (error) {{
                            console.error('Payment error:', error);
                            errorMessage.textContent = error.message;
                            errorMessage.style.display = 'block';
                            submitButton.disabled = false;
                            buttonSpinner.style.display = 'none';
                            buttonText.textContent = 'Pay Again';
                            
                            try {{
                                console.log('Attempting to send error message to C#...');
                                window.chrome.webview.postMessage(JSON.stringify({{
                                    type: 'payment_error',
                                    message: error.message
                                }}));
                                console.log('Error message sent successfully');
                            }} catch (postError) {{
                                console.error('Failed to send error message:', postError);
                            }}
                        }} else if (paymentIntent && paymentIntent.status === 'succeeded') {{
                            console.log('Payment succeeded! Payment Intent:', paymentIntent);
                            
                            try {{
                                if (!window.chrome || !window.chrome.webview) {{
                                    throw new Error('WebView2 communication not available');
                                }}
                                window.chrome.webview.postMessage(JSON.stringify({{ type: 'payment_success' }}));
                            }} catch (postError) {{
                                console.error('Failed to send success message to C#:', postError);
                                errorMessage.textContent = 'Payment succeeded but failed to communicate with application: ' + postError.message;
                                errorMessage.style.display = 'block';
                                submitButton.disabled = false;
                                buttonSpinner.style.display = 'none';
                                buttonText.textContent = 'Pay Again';
                            }}
                        }}
                    }} catch (e) {{
                        console.error('Exception:', e);
                        errorMessage.textContent = 'An error occurred: ' + e.message;
                        errorMessage.style.display = 'block';
                        submitButton.disabled = false;
                        buttonSpinner.style.display = 'none';
                        buttonText.textContent = 'Pay Again';
                    }}
                }});
                
                console.log('Payment form ready!');
                
            }} catch (error) {{
                console.error('Fatal error:', error);
                document.getElementById('error-message').textContent = 'Failed to initialize payment form: ' + error.message;
                document.getElementById('error-message').style.display = 'block';
            }}
        }})();
    </script>
</body>
</html>";
        }

        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorMessageText.Visibility = Visibility.Visible;
            StatusText.Foreground = (SolidColorBrush)FindResource("Brush.Error");
            StatusText.Text = "Payment failed. Please try again.";
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private class PaymentMessage
        {
            public string Type { get; set; }
            public string Message { get; set; }
        }
    }

    public class PaymentSuccessEventArgs : EventArgs
    {
        public string PlanName { get; }
        public int DurationDays { get; }
        public decimal Price { get; }
        public string PaymentIntentId { get; }

        public PaymentSuccessEventArgs(string planName, int durationDays, decimal price, string paymentIntentId)
        {
            PlanName = planName;
            DurationDays = durationDays;
            Price = price;
            PaymentIntentId = paymentIntentId;
        }
    }
}
