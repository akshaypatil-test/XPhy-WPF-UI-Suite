#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly DeviceFingerprintService _fingerprintService;

        public AuthService()
        {
            //_baseUrl = "http://localhost:5163";
            _baseUrl = "https://xphy-web-c5e3v.ondigitalocean.app";
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _fingerprintService = new DeviceFingerprintService();

            // Ignore SSL certificate errors for localhost (development only)
            // Note: This is only for development. Remove in production!
            ServicePointManager.ServerCertificateValidationCallback += 
                (sender, certificate, chain, sslPolicyErrors) => true;
        }

        public async Task<LoginResponse?> LoginAsync(string username, string password, string? licenseKey = null, bool rememberMe = false)
        {
            try
            {
                var deviceFingerprint = _fingerprintService.GetDeviceFingerprint();
                var request = new LoginRequest
                {
                    Username = username,
                    Password = password,
                    DeviceFingerprint = deviceFingerprint,
                    LicenseKey = string.IsNullOrWhiteSpace(licenseKey) ? null : licenseKey!.Trim(),
                    RememberMe = rememberMe
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/auth/login", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(responseJson);
                    return loginResponse;
                }
                else
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                    throw new Exception(errorResponse?.Message ?? $"Login failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Login error: {ex.Message}");
            }
        }

        /// <summary>Exchange refresh token for a new access token. Returns null if refresh token is invalid or expired.</summary>
        public async Task<RefreshTokenResponse?> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;
            try
            {
                var request = new RefreshTokenRequest { RefreshToken = refreshToken.Trim() };
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/auth/refresh", content);
                if (!response.IsSuccessStatusCode)
                    return null;
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<RefreshTokenResponse>(responseJson);
            }
            catch
            {
                return null;
            }
        }

        public async Task<RegisterResponse> RegisterAsync(string username, string password, string firstName, string lastName)
        {
            try
            {
                var request = new RegisterRequest
                {
                    Username = username,
                    Password = password,
                    FirstName = firstName?.Trim() ?? string.Empty,
                    LastName = lastName?.Trim() ?? string.Empty,
                    UserType = UserType.NonCorp
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/auth/register", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var registerResponse = JsonConvert.DeserializeObject<RegisterResponse>(responseJson);
                    if (registerResponse == null)
                    {
                        throw new Exception("Invalid response from server.");
                    }
                    return registerResponse;
                }
                else
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                    throw new Exception(errorResponse?.Message ?? $"Registration failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Registration error: {ex.Message}");
            }
        }

        public async Task<RegisterResponse> RegisterCorpUserAsync(
            string username,
            string password,
            string firstName,
            string lastName,
            int maxDevices,
            string policyNumber,
            string organizationName,
            string contactPersonName,
            string countryCode,
            string contactNumber,
            string orderNumber,
            string activationDate)
        {
            try
            {
                var request = new RegisterRequest
                {
                    Username = username,
                    Password = password,
                    FirstName = firstName?.Trim() ?? string.Empty,
                    LastName = lastName?.Trim() ?? string.Empty,
                    UserType = Models.UserType.Corp,
                    MaxDevices = maxDevices,
                    PolicyNumber = string.IsNullOrWhiteSpace(policyNumber) ? null : policyNumber.Trim(),
                    OrganizationName = string.IsNullOrWhiteSpace(organizationName) ? null : organizationName.Trim(),
                    ContactPersonName = string.IsNullOrWhiteSpace(contactPersonName) ? null : contactPersonName.Trim(),
                    CountryCode = string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim(),
                    ContactNumber = string.IsNullOrWhiteSpace(contactNumber) ? null : contactNumber.Trim(),
                    OrderNumber = string.IsNullOrWhiteSpace(orderNumber) ? null : orderNumber.Trim(),
                    ActivationDate = string.IsNullOrWhiteSpace(activationDate) ? null : activationDate.Trim()
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/auth/register", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var registerResponse = JsonConvert.DeserializeObject<RegisterResponse>(responseJson);
                    if (registerResponse == null)
                    {
                        throw new Exception("Invalid response from server.");
                    }
                    return registerResponse;
                }
                else
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                    throw new Exception(errorResponse?.Message ?? $"Registration failed: {response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Registration error: {ex.Message}");
            }
        }

        public async Task<VerifyEmailResponse> VerifyEmailAsync(string email, string code)
        {
            try
            {
                var request = new VerifyEmailRequest { Email = email, Code = code };
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/auth/verify-email", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var verifyResponse = JsonConvert.DeserializeObject<VerifyEmailResponse>(responseJson);
                    if (verifyResponse == null)
                        throw new Exception("Invalid response from server.");
                    return verifyResponse;
                }

                var errorJson = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                throw new Exception(errorResponse?.Message ?? $"Verification failed: {response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Verification error: {ex.Message}");
            }
        }

        public async Task<ResendOtpResponse> ResendOtpAsync(string email)
        {
            try
            {
                var request = new ResendOtpRequest { Email = email };
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/auth/resend-otp", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var resendResponse = JsonConvert.DeserializeObject<ResendOtpResponse>(responseJson);
                    return resendResponse ?? new ResendOtpResponse { Message = "Code resent." };
                }

                var errorJson = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                throw new Exception(errorResponse?.Message ?? "Failed to resend code.");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Resend error: {ex.Message}");
            }
        }

        public async Task<ForgotUsernameResponse> ForgotUsernameAsync(string email)
        {
            try
            {
                var request = new ForgotUsernameRequest { Email = email.Trim() };
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/auth/forgot-username", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var forgotResponse = JsonConvert.DeserializeObject<ForgotUsernameResponse>(responseJson);
                    if (forgotResponse == null)
                        throw new Exception("Invalid response from server.");
                    return forgotResponse;
                }

                var errorJson = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                throw new Exception(errorResponse?.Message ?? $"Username recovery failed: {response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Username recovery error: {ex.Message}");
            }
        }

        public async Task<ForgotPasswordResponse> ForgotPasswordAsync(string email)
        {
            try
            {
                var request = new ForgotPasswordRequest { Email = email.Trim() };
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/auth/forgot-password", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var forgotResponse = JsonConvert.DeserializeObject<ForgotPasswordResponse>(responseJson);
                    if (forgotResponse == null)
                        throw new Exception("Invalid response from server.");
                    return forgotResponse;
                }

                var errorJson = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                throw new Exception(errorResponse?.Message ?? $"Password reset request failed: {response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Password reset error: {ex.Message}");
            }
        }

        public async Task<VerifyPasswordResetOtpResponse> VerifyPasswordResetOtpAsync(string email, string code)
        {
            try
            {
                var request = new VerifyPasswordResetOtpRequest { Email = email.Trim(), Code = code.Trim() };
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/auth/verify-password-reset-otp", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var verifyResponse = JsonConvert.DeserializeObject<VerifyPasswordResetOtpResponse>(responseJson);
                    if (verifyResponse == null)
                        throw new Exception("Invalid response from server.");
                    return verifyResponse;
                }

                var errorJson = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                throw new Exception(errorResponse?.Message ?? "Invalid or expired code.");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<ResetPasswordResponse> ResetPasswordAsync(string resetToken, string newPassword)
        {
            try
            {
                var request = new ResetPasswordRequest { ResetToken = resetToken, NewPassword = newPassword };
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/auth/reset-password", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var resetResponse = JsonConvert.DeserializeObject<ResetPasswordResponse>(responseJson);
                    if (resetResponse == null)
                        throw new Exception("Invalid response from server.");
                    return resetResponse;
                }

                var errorJson = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                throw new Exception(errorResponse?.Message ?? "Password reset failed.");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>Change password when logged in (e.g. first-time corporate login). Requires Bearer token.</summary>
        public async Task<ChangePasswordResponse> ChangePasswordAsync(string currentPassword, string newPassword, string accessToken)
        {
            try
            {
                var request = new ChangePasswordRequest { CurrentPassword = currentPassword, UpdatedPassword = newPassword };
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password") { Content = content };
                msg.Headers.Add("Authorization", "Bearer " + accessToken);
                var response = await _httpClient.SendAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var changeResponse = JsonConvert.DeserializeObject<ChangePasswordResponse>(responseJson);
                    return changeResponse ?? new ChangePasswordResponse { Success = true, Message = "Password updated." };
                }

                var errorJson = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
                throw new Exception(errorResponse?.Message ?? "Password change failed.");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Request timeout. Please check your connection.");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>Request change-password OTP; sends code to logged-in user's email. Requires Bearer token.</summary>
        public async Task<RequestChangePasswordOtpResponse> RequestChangePasswordOtpAsync(string currentPassword, string newPassword, string accessToken)
        {
            var request = new ChangePasswordRequest { CurrentPassword = currentPassword, UpdatedPassword = newPassword };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/auth/request-change-password-otp") { Content = content };
            msg.Headers.Add("Authorization", "Bearer " + accessToken);
            var response = await _httpClient.SendAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var o = JsonConvert.DeserializeObject<RequestChangePasswordOtpResponse>(responseJson);
                return o ?? new RequestChangePasswordOtpResponse { Success = true, Message = "Code sent." };
            }

            var errorJson = await response.Content.ReadAsStringAsync();
            var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
            throw new Exception(errorResponse?.Message ?? "Failed to send verification code.");
        }

        /// <summary>Verify change-password OTP and complete password update. Requires Bearer token.</summary>
        public async Task<VerifyChangePasswordOtpResponse> VerifyChangePasswordOtpAsync(string code, string accessToken)
        {
            var request = new VerifyChangePasswordOtpRequest { Code = code };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/auth/verify-change-password-otp") { Content = content };
            msg.Headers.Add("Authorization", "Bearer " + accessToken);
            var response = await _httpClient.SendAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var o = JsonConvert.DeserializeObject<VerifyChangePasswordOtpResponse>(responseJson);
                return o ?? new VerifyChangePasswordOtpResponse { Success = true, Message = "Password changed." };
            }

            var errorJson = await response.Content.ReadAsStringAsync();
            var errorResponse = JsonConvert.DeserializeObject<ApiErrorResponse>(errorJson);
            throw new Exception(errorResponse?.Message ?? "Verification failed.");
        }
    }
}
