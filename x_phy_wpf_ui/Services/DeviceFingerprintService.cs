using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace x_phy_wpf_ui.Services
{
    public class DeviceFingerprintService
    {
        private static string _cachedFingerprint;

        /// <summary>
        /// Generates a unique device fingerprint based on machine characteristics
        /// </summary>
        public string GetDeviceFingerprint()
        {
            if (!string.IsNullOrEmpty(_cachedFingerprint))
            {
                return _cachedFingerprint;
            }

            try
            {
                var components = new StringBuilder();

                components.Append(Environment.MachineName).Append("|");
                components.Append(Environment.OSVersion).Append("|");
                components.Append(Environment.ProcessorCount).Append("|");
                components.Append(Environment.UserName).Append("|");

                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        var productId = key?.GetValue("ProductId")?.ToString();
                        if (!string.IsNullOrEmpty(productId))
                        {
                            components.Append(productId);
                        }
                    }
                }
                catch
                {
                    components.Append(Environment.UserDomainName);
                }

                var data = Encoding.UTF8.GetBytes(components.ToString());

                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(data);

                    // ✅ Base64 URL-safe encoding (matches API regex)
                    _cachedFingerprint = Convert
                        .ToBase64String(hash)
                        .Replace("+", "-")
                        .Replace("/", "_")
                        .TrimEnd('=');
                }

                return _cachedFingerprint;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Error generating device fingerprint: {ex.Message}");

                // Fallback ALSO regex-safe
                _cachedFingerprint =
                    $"DEVICE_{Environment.MachineName}_{Environment.UserName}"
                        .Replace(" ", "_");

                return _cachedFingerprint;
            }
        }

    }
}
