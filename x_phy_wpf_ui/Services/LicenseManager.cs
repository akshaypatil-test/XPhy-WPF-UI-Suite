using System;
using System.IO;
using Newtonsoft.Json;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    public class LicenseManager
    {
        private static readonly string LicenseFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "X-PHY",
            "X-PHY Deepfake Detector",
            "license.json"
        );

        public UserLicense GetCurrentLicense()
        {
            try
            {
                if (File.Exists(LicenseFilePath))
                {
                    string json = File.ReadAllText(LicenseFilePath);
                    var license = JsonConvert.DeserializeObject<UserLicense>(json);
                    
                    if (license != null && !license.IsExpired)
                    {
                        license.IsActive = true;
                        return license;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading license: {ex.Message}");
            }

            return new UserLicense
            {
                IsActive = false,
                PurchaseDate = DateTime.MinValue,
                ExpiryDate = DateTime.MinValue,
                PlanName = "Trial"
            };
        }

        public bool SaveLicense(UserLicense license)
        {
            try
            {
                string directory = Path.GetDirectoryName(LicenseFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(license, Formatting.Indented);
                File.WriteAllText(LicenseFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving license: {ex.Message}");
                return false;
            }
        }

        public bool ActivateLicense(string planName, int durationDays, decimal amountPaid, string transactionId)
        {
            var license = new UserLicense
            {
                IsActive = true,
                PurchaseDate = DateTime.Now,
                ExpiryDate = DateTime.Now.AddDays(durationDays),
                PlanName = planName,
                AmountPaid = amountPaid,
                TransactionId = transactionId
            };

            return SaveLicense(license);
        }

        public bool HasValidLicense()
        {
            var license = GetCurrentLicense();
            return license.IsActive && !license.IsExpired;
        }
    }
}
