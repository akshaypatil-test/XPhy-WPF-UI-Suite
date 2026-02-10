using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace InstallerUI
{
    /// <summary>
    /// Writes to the interactive (logged-in) user's HKCU Run key so that "launch on startup"
    /// works even when the installer runs elevated (as Administrator). Without this,
    /// Registry.CurrentUser would be the elevated user's hive, not the user who will log in.
    /// </summary>
    internal static class InteractiveUserRunKey
    {
        private const int WTS_CURRENT_SERVER_HANDLE = 0;
        private const int WTSUserName = 5;
        private const int WTSDomainName = 7;

        [DllImport("wtsapi32.dll", SetLastError = false)]
        private static extern bool WTSQuerySessionInformation(IntPtr server, int sessionId, int wtsInfoClass, out IntPtr buffer, out int bytesReturned);

        [DllImport("wtsapi32.dll", SetLastError = false)]
        private static extern void WTSFreeMemory(IntPtr pointer);

        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern int WTSGetActiveConsoleSessionId();

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupAccountName(string systemName, string accountName, IntPtr sid, ref int sidSize, StringBuilder domain, ref int domainSize, out int sidType);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ConvertSidToStringSid(IntPtr sid, out IntPtr sidString);

        [DllImport("advapi32.dll", SetLastError = false)]
        private static extern IntPtr LocalFree(IntPtr ptr);

        private const int SID_MAX_SUB_AUTHORITIES = 15;
        private const int SECURITY_MAX_SID_SIZE = 68; // 12 + (SID_MAX_SUB_AUTHORITIES * 4)

        /// <summary>Try to open the interactive user's Run key (HKEY_USERS\&lt;interactive user SID&gt;\...\Run). Returns null if not available.</summary>
        public static RegistryKey OpenInteractiveUserRunKey(bool writable)
        {
            try
            {
                string sidString = GetInteractiveUserSidString();
                if (string.IsNullOrEmpty(sidString)) return null;

                var usersKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
                try
                {
                    var runPath = sidString + @"\Software\Microsoft\Windows\CurrentVersion\Run";
                    return usersKey.OpenSubKey(runPath, writable);
                }
                finally
                {
                    usersKey?.Dispose();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Get the interactive user's Desktop folder path (e.g. C:\Users\John\Desktop). Returns null if not available.</summary>
        public static string GetInteractiveUserDesktopPath()
        {
            try
            {
                string sidString = GetInteractiveUserSidString();
                if (string.IsNullOrEmpty(sidString)) return null;

                using (var usersKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default))
                using (var shellFolders = usersKey.OpenSubKey(sidString + @"\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"))
                {
                    var desktop = shellFolders?.GetValue("Desktop") as string;
                    return string.IsNullOrEmpty(desktop) ? null : desktop;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string GetInteractiveUserSidString()
        {
            try
            {
                int sessionId = WTSGetActiveConsoleSessionId();
                if (sessionId <= 0) return null;

                string domain = GetWtsSessionString(sessionId, WTSDomainName);
                string user = GetWtsSessionString(sessionId, WTSUserName);
                if (string.IsNullOrEmpty(user)) return null;

                string accountName = string.IsNullOrEmpty(domain) ? user : domain + "\\" + user;
                return GetSidStringForAccount(accountName);
            }
            catch
            {
                return null;
            }
        }

        private static string GetWtsSessionString(int sessionId, int wtsInfoClass)
        {
            IntPtr buffer = IntPtr.Zero;
            int bytesReturned = 0;
            try
            {
                if (!WTSQuerySessionInformation(IntPtr.Zero, sessionId, wtsInfoClass, out buffer, out bytesReturned))
                    return null;
                if (buffer == IntPtr.Zero || bytesReturned < 2) return null;
                return Marshal.PtrToStringAuto(buffer);
            }
            finally
            {
                if (buffer != IntPtr.Zero) WTSFreeMemory(buffer);
            }
        }

        private static string GetSidStringForAccount(string accountName)
        {
            IntPtr sid = Marshal.AllocHGlobal(SECURITY_MAX_SID_SIZE);
            try
            {
                int sidSize = SECURITY_MAX_SID_SIZE;
                var domainBuilder = new StringBuilder(256);
                int domainSize = domainBuilder.Capacity;
                if (!LookupAccountName(null, accountName, sid, ref sidSize, domainBuilder, ref domainSize, out _))
                    return null;
                if (!ConvertSidToStringSid(sid, out IntPtr sidStringPtr))
                    return null;
                try
                {
                    return Marshal.PtrToStringAuto(sidStringPtr);
                }
                finally
                {
                    if (sidStringPtr != IntPtr.Zero) LocalFree(sidStringPtr);
                }
            }
            finally
            {
                if (sid != IntPtr.Zero) Marshal.FreeHGlobal(sid);
            }
        }
    }
}
