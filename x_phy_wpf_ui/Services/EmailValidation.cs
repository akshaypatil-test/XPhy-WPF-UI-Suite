namespace x_phy_wpf_ui.Services
{
    /// <summary>Shared rules for email/username fields (login credential is email).</summary>
    public static class EmailValidation
    {
        public const int MinLocalPartLength = 5;

        /// <summary>True if <paramref name="email"/> contains @ with a non-empty local part before it.</summary>
        public static bool TryGetLocalPart(string email, out string localPart)
        {
            localPart = "";
            if (string.IsNullOrWhiteSpace(email)) return false;
            var at = email.IndexOf('@');
            if (at <= 0) return false;
            localPart = email.Substring(0, at).Trim();
            return true;
        }

        /// <summary>True if the part before the first @ is at least <paramref name="minLength"/> characters.</summary>
        public static bool IsLocalPartAtLeast(string email, int minLength = MinLocalPartLength)
        {
            return TryGetLocalPart(email, out var local) && local.Length >= minLength;
        }
    }
}
