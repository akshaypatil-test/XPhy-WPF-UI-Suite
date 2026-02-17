using System;
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

            PlanDetailsText.Text = $"{plan.Name} Plan - ${plan.Price:F2} ({plan.DurationDays} days)";

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
                
                StatusText.Text = "Enter your card details below";
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
                            StatusText.Foreground = (SolidColorBrush)FindResource("SecondaryTextColor");
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
            background: #ffffff;
            padding: 20px;
            min-height: 100vh;
        }}
        .amount-display {{
            background: #f6f9fc;
            border: 1px solid #e3e8ee;
            border-radius: 8px;
            padding: 16px;
            margin-bottom: 24px;
            text-align: center;
        }}
        .amount-label {{
            font-size: 14px;
            color: #6b7c93;
            margin-bottom: 4px;
        }}
        .amount-value {{
            font-size: 32px;
            font-weight: bold;
            color: #E2156B;
        }}
        #submit-button {{
            background: #E2156B;
            color: white;
            border: none;
            border-radius: 8px;
            padding: 14px 24px;
            font-size: 16px;
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
            color: #e74c3c;
            font-size: 14px;
            margin-top: 12px;
            padding: 12px;
            background: #fee;
            border-radius: 4px;
            display: none;
        }}
        .spinner {{
            display: inline-block;
            width: 16px;
            height: 16px;
            border: 3px solid rgba(255,255,255,.3);
            border-radius: 50%;
            border-top-color: #fff;
            animation: spin 0.8s ease-in-out infinite;
            margin-left: 8px;
        }}
        @keyframes spin {{
            to {{ transform: rotate(360deg); }}
        }}
    </style>
</head>
<body>
    <div class=""amount-display"">
        <div class=""amount-label"">Amount to pay</div>
        <div class=""amount-value"">${amount:F2}</div>
    </div>

    <form id=""payment-form"">
        <div style=""margin-bottom: 16px;"">
            <label style=""display: block; font-size: 13px; font-weight: 600; margin-bottom: 6px; color: #374151;"">Card Number</label>
            <div id=""card-number-element"" style=""border: 1px solid #d1d5db; border-radius: 8px; padding: 12px;""></div>
        </div>
        
        <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 16px;"">
            <div>
                <label style=""display: block; font-size: 13px; font-weight: 600; margin-bottom: 6px; color: #374151;"">Expiry Date</label>
                <div id=""card-expiry-element"" style=""border: 1px solid #d1d5db; border-radius: 8px; padding: 12px;""></div>
            </div>
            <div>
                <label style=""display: block; font-size: 13px; font-weight: 600; margin-bottom: 6px; color: #374151;"">CVC</label>
                <div id=""card-cvc-element"" style=""border: 1px solid #d1d5db; border-radius: 8px; padding: 12px;""></div>
            </div>
        </div>
        
        <div style=""margin-bottom: 20px;"">
            <label style=""display: block; font-size: 13px; font-weight: 600; margin-bottom: 6px; color: #374151;"">ZIP Code</label>
            <div id=""card-postal-element"" style=""border: 1px solid #d1d5db; border-radius: 8px; padding: 12px;""></div>
        </div>
        
        <button id=""submit-button"" type=""submit"">
            <span id=""button-text"">Pay ${amount:F2}</span>
            <span id=""button-spinner"" class=""spinner"" style=""display:none;""></span>
        </button>
        <div id=""error-message""></div>
    </form>

    <script>
        (function() {{
            try {{
                console.log('Starting Stripe initialization...');
                console.log('Stripe key:', '{StripePaymentService.StripePublishableKey}'.substring(0, 20) + '...');
                console.log('Client secret:', '{clientSecret}'.substring(0, 20) + '...');
                
                // Check if Stripe.js loaded
                if (typeof Stripe === 'undefined') {{
                    throw new Error('Stripe.js failed to load');
                }}
                
                // Initialize Stripe
                const stripe = Stripe('{StripePaymentService.StripePublishableKey}');
                console.log('Stripe object created');
                
                // Create individual card elements for better UI
                const elements = stripe.elements();
                
                const cardNumberElement = elements.create('cardNumber', {{
                    style: {{
                        base: {{
                            fontSize: '16px',
                            fontFamily: 'Segoe UI, Arial, sans-serif',
                            color: '#111827',
                            '::placeholder': {{ color: '#9ca3af' }}
                        }},
                        invalid: {{ 
                            color: '#dc2626',
                            iconColor: '#dc2626'
                        }}
                    }}
                }});
                
                const cardExpiryElement = elements.create('cardExpiry', {{
                    style: {{
                        base: {{
                            fontSize: '16px',
                            fontFamily: 'Segoe UI, Arial, sans-serif',
                            color: '#111827',
                            '::placeholder': {{ color: '#9ca3af' }}
                        }},
                        invalid: {{ 
                            color: '#dc2626',
                            iconColor: '#dc2626'
                        }}
                    }}
                }});
                
                const cardCvcElement = elements.create('cardCvc', {{
                    style: {{
                        base: {{
                            fontSize: '16px',
                            fontFamily: 'Segoe UI, Arial, sans-serif',
                            color: '#111827',
                            '::placeholder': {{ color: '#9ca3af' }}
                        }},
                        invalid: {{ 
                            color: '#dc2626',
                            iconColor: '#dc2626'
                        }}
                    }}
                }});
                
                const postalCodeElement = elements.create('postalCode', {{
                    style: {{
                        base: {{
                            fontSize: '16px',
                            fontFamily: 'Segoe UI, Arial, sans-serif',
                            color: '#111827',
                            '::placeholder': {{ color: '#9ca3af' }}
                        }},
                        invalid: {{ 
                            color: '#dc2626',
                            iconColor: '#dc2626'
                        }}
                    }}
                }});
                
                // Mount elements to their containers
                cardNumberElement.mount('#card-number-element');
                cardExpiryElement.mount('#card-expiry-element');
                cardCvcElement.mount('#card-cvc-element');
                postalCodeElement.mount('#card-postal-element');
                console.log('Card elements mounted');
                
                const form = document.getElementById('payment-form');
                const submitButton = document.getElementById('submit-button');
                const buttonText = document.getElementById('button-text');
                const buttonSpinner = document.getElementById('button-spinner');
                const errorMessage = document.getElementById('error-message');
                
                // Handle card validation errors for all elements
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
                
                // Handle form submission
                form.addEventListener('submit', async function(event) {{
                    event.preventDefault();
                    
                    submitButton.disabled = true;
                    buttonSpinner.style.display = 'inline-block';
                    errorMessage.style.display = 'none';
                    
                    try {{
                        // Notify C# that processing started
                        try {{
                            console.log('Attempting to send processing message to C#...');
                            window.chrome.webview.postMessage(JSON.stringify({{
                                type: 'payment_processing'
                            }}));
                            console.log('Processing message sent successfully');
                        }} catch (postError) {{
                            console.error('Failed to send processing message:', postError);
                        }}
                        
                        console.log('Confirming card payment...');
                        const {{error, paymentIntent}} = await stripe.confirmCardPayment(
                            '{clientSecret}',
                            {{
                                payment_method: {{ 
                                    card: cardNumberElement
                                }}
                            }}
                        );
                        
                        if (error) {{
                            console.error('Payment error:', error);
                            errorMessage.textContent = error.message;
                            errorMessage.style.display = 'block';
                            submitButton.disabled = false;
                            buttonSpinner.style.display = 'none';
                            
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
                                console.log('Checking webview availability...');
                                console.log('window.chrome:', typeof window.chrome);
                                console.log('window.chrome.webview:', typeof window.chrome?.webview);
                                
                                if (!window.chrome || !window.chrome.webview) {{
                                    throw new Error('WebView2 communication not available');
                                }}
                                
                                console.log('Attempting to send success message to C#...');
                                window.chrome.webview.postMessage(JSON.stringify({{
                                    type: 'payment_success'
                                }}));
                                console.log('Success message sent to C#!');
                                
                                // Visual feedback
                                buttonText.textContent = 'Payment Successful!';
                                buttonSpinner.style.display = 'none';
                                errorMessage.style.display = 'none';
                            }} catch (postError) {{
                                console.error('Failed to send success message to C#:', postError);
                                errorMessage.textContent = 'Payment succeeded but failed to communicate with application: ' + postError.message;
                                errorMessage.style.display = 'block';
                            }}
                        }}
                    }} catch (e) {{
                        console.error('Exception:', e);
                        errorMessage.textContent = 'An error occurred: ' + e.message;
                        errorMessage.style.display = 'block';
                        submitButton.disabled = false;
                        buttonSpinner.style.display = 'none';
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
            StatusText.Foreground = (SolidColorBrush)FindResource("ErrorColor");
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
