#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using x_phy_wpf_ui.Models;

namespace x_phy_wpf_ui.Services
{
    public class ProcessDetectionService
    {
        // Windows API declarations
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd); // Check if window is minimized

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd); // Check if handle is valid window

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // Video calling applications
        private static readonly HashSet<string> VideoCallingProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Zoom.exe",
            "CptHost.exe", // Zoom's process name
            "ms-teams.exe",
            "Teams.exe",
            "Skype.exe",
            "SkypeApp.exe",
            "SkypeHost.exe",
            "Meet.exe", // Google Meet standalone
            "msedgewebview2.exe", // Teams Chat
            "msedge.exe" // Teams Chat (sometimes runs as Edge)
        };

        // Media players
        private static readonly HashSet<string> MediaPlayerProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "vlc.exe",
            "wmplayer.exe", // Windows Media Player
            "wmpshare.exe",
            "MediaPlayer.exe"
        };

        // Browsers (for YouTube detection)
        private static readonly HashSet<string> BrowserProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "chrome.exe",
            "msedge.exe",
            "firefox.exe"
        };

        public List<DetectedProcess> DetectRelevantProcesses()
        {
            var detectedProcesses = new List<DetectedProcess>();
            var processWindows = new Dictionary<uint, List<string>>(); // ProcessId -> Window Titles
            var addedProcessIds = new HashSet<int>(); // Track ProcessIds to prevent duplicates
            var checkedBrowsers = new HashSet<string>(); // Track which browsers we've already checked

            // Enumerate all windows to get window titles for each process
            // Include both visible and minimized windows (but exclude hidden/background windows)
            EnumWindows((hWnd, lParam) =>
            {
                // Check if window is valid and either visible or minimized (but not hidden)
                if (IsWindow(hWnd) && (IsWindowVisible(hWnd) || IsIconic(hWnd)))
                {
                    int length = GetWindowTextLength(hWnd);
                    if (length > 0)
                    {
                        StringBuilder windowTitle = new StringBuilder(length + 1);
                        GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                        string title = windowTitle.ToString();

                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            GetWindowThreadProcessId(hWnd, out uint processId);
                            if (!processWindows.ContainsKey(processId))
                            {
                                processWindows[processId] = new List<string>();
                            }
                            processWindows[processId].Add(title);
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            // Get all running processes
            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                try
                {
                    string processName = process.ProcessName + ".exe";
                    DetectedProcess? detected = null;

                    // Only process applications that have a main window handle (foreground apps, even if minimized)
                    // This filters out true background processes while including minimized apps
                    try
                    {
                        IntPtr mainWindowHandle = process.MainWindowHandle;
                        string mainWindowTitle = process.MainWindowTitle;
                        
                        // Skip if no main window handle or empty title (true background process)
                        if (mainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(mainWindowTitle))
                        {
                            // Also check if we found windows through enumeration (for processes with multiple windows)
                            if (!processWindows.ContainsKey((uint)process.Id) || processWindows[(uint)process.Id].Count == 0)
                            {
                                continue; // Skip processes without windows
                            }
                        }
                    }
                    catch
                    {
                        // If we can't access MainWindowHandle, check enumeration results
                        if (!processWindows.ContainsKey((uint)process.Id) || processWindows[(uint)process.Id].Count == 0)
                        {
                            continue; // Skip processes without windows
                        }
                    }

                    // Check for video calling applications
                    if (VideoCallingProcesses.Contains(processName))
                    {
                        // Additional check for Teams Chat - msedgewebview2.exe or msedge.exe might be Teams Chat
                        if (processName.Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase) ||
                            processName.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            // Check if window titles contain Teams-related text
                            var windowTitles = processWindows[(uint)process.Id];
                            
                            bool isTeamsChat = windowTitles.Any(title => 
                                title.IndexOf("Teams", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                title.IndexOf("Microsoft Teams", StringComparison.OrdinalIgnoreCase) >= 0);
                            
                            if (!isTeamsChat)
                            {
                                // Skip this instance if it's not Teams
                                continue;
                            }
                        }
                        
                        string displayName = GetDisplayName(processName);
                        detected = new DetectedProcess
                        {
                            ProcessId = process.Id,
                            ProcessName = processName,
                            DisplayName = displayName,
                            ProcessType = "VideoCalling",
                            HasYouTubeTab = false,
                            WindowTitle = GetMainWindowTitle(process, processWindows)
                        };
                    }
                    // Check for media players
                    else if (MediaPlayerProcesses.Contains(processName))
                    {
                        string displayName = GetDisplayName(processName);
                        detected = new DetectedProcess
                        {
                            ProcessId = process.Id,
                            ProcessName = processName,
                            DisplayName = displayName,
                            ProcessType = "MediaPlayer",
                            HasYouTubeTab = false,
                            WindowTitle = GetMainWindowTitle(process, processWindows)
                        };
                    }
                    // Check for browsers with YouTube tabs
                    else if (BrowserProcesses.Contains(processName))
                    {
                        // Skip if we've already checked this browser type
                        if (checkedBrowsers.Contains(processName))
                        {
                            continue;
                        }

                        // For browsers, check all processes of the same type for YouTube tabs
                        // This is especially important for Chrome which uses multiple processes
                        var allBrowserProcesses = processes
                            .Where(p => (p.ProcessName + ".exe").Equals(processName, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        DetectedProcess? browserDetected = null;
                        foreach (var browserProcess in allBrowserProcesses)
                        {
                            if (processWindows.ContainsKey((uint)browserProcess.Id))
                            {
                                bool hasYouTube = HasYouTubeTab(processWindows[(uint)browserProcess.Id]);
                                if (hasYouTube)
                                {
                                    string displayName = GetDisplayName(processName);
                                    browserDetected = new DetectedProcess
                                    {
                                        ProcessId = browserProcess.Id, // Use the process ID that has the YouTube tab
                                        ProcessName = processName,
                                        DisplayName = displayName,
                                        ProcessType = "Browser",
                                        HasYouTubeTab = true,
                                        WindowTitle = GetYouTubeWindowTitle(processWindows[(uint)browserProcess.Id])
                                    };
                                    break; // Found YouTube, no need to check other processes of this browser
                                }
                            }
                        }

                        if (browserDetected != null)
                        {
                            detected = browserDetected;
                        }

                        // Mark this browser type as checked
                        checkedBrowsers.Add(processName);
                    }

                    if (detected != null)
                    {
                        // Prevent duplicate processes by ProcessId
                        if (!addedProcessIds.Contains(detected.ProcessId))
                        {
                            addedProcessIds.Add(detected.ProcessId);
                            detectedProcesses.Add(detected);
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip processes we can't access
                    continue;
                }
            }

            // Remove duplicates by DisplayName (in case same app appears with different ProcessIds)
            // Group by DisplayName and take the first occurrence of each
            var uniqueByDisplayName = detectedProcesses
                .GroupBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            // Group browsers together - only show one browser if multiple browsers are detected
            var browsers = uniqueByDisplayName.Where(p => p.ProcessType == "Browser").ToList();
            var nonBrowsers = uniqueByDisplayName.Where(p => p.ProcessType != "Browser").ToList();
            
            // If we have browsers, only take the first one
            var selectedBrowsers = browsers.Any() ? browsers.Take(1).ToList() : new List<DetectedProcess>();
            
            // Combine non-browsers with one browser
            var combined = nonBrowsers.Concat(selectedBrowsers).ToList();
            
            // Sort by priority: VideoCalling > MediaPlayer > Browser, then take top 3
            var sorted = combined
                .OrderByDescending(p => p.ProcessType == "VideoCalling" ? 3 : p.ProcessType == "MediaPlayer" ? 2 : 1)
                .Take(3)
                .ToList();

            return sorted;
        }

        private string GetDisplayName(string processName)
        {
            // For msedge.exe, check if it's Teams Chat by checking window titles
            // This is called after we've already verified it's Teams Chat
            if (processName.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase))
            {
                return "Microsoft Teams Chat";
            }
            
            return processName.ToLower() switch
            {
                "zoom.exe" or "cpthost.exe" => "Zoom",
                "ms-teams.exe" or "teams.exe" => "Microsoft Teams",
                "msedgewebview2.exe" => "Microsoft Teams Chat",
                "skype.exe" or "skypeapp.exe" or "skypehost.exe" => "Skype",
                "meet.exe" => "Google Meet",
                "vlc.exe" => "VLC Media Player",
                "wmplayer.exe" or "wmpshare.exe" or "mediaplayer.exe" => "Windows Media Player",
                "chrome.exe" => "Google Chrome",
                "firefox.exe" => "Mozilla Firefox",
                _ => processName.Replace(".exe", "")
            };
        }

        private string GetMainWindowTitle(Process process, Dictionary<uint, List<string>> processWindows)
        {
            try
            {
                if (processWindows.ContainsKey((uint)process.Id) && processWindows[(uint)process.Id].Count > 0)
                {
                    return processWindows[(uint)process.Id][0];
                }
                return process.MainWindowTitle;
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool HasYouTubeTab(List<string> windowTitles)
        {
            foreach (var title in windowTitles)
            {
                if (title.IndexOf("YouTube", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    title.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private string GetYouTubeWindowTitle(List<string> windowTitles)
        {
            foreach (var title in windowTitles)
            {
                if (title.IndexOf("YouTube", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    title.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return title;
                }
            }
            return windowTitles.Count > 0 ? windowTitles[0] : string.Empty;
        }

        /// <summary>
        /// Gets the main window handle for a process. Tries MainWindowHandle first, then enumerates windows.
        /// </summary>
        public static IntPtr GetWindowHandleForProcess(int processId)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                        return process.MainWindowHandle;
                }
            }
            catch { }

            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if ((int)pid == processId && IsWindow(hWnd) && (IsWindowVisible(hWnd) || IsIconic(hWnd)))
                {
                    if (GetWindowTextLength(hWnd) > 0)
                    {
                        found = hWnd;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        /// <summary>
        /// Brings the main window of the given process to the foreground. Restores if minimized.
        /// </summary>
        public static void BringProcessWindowToForeground(int processId)
        {
            IntPtr hWnd = GetWindowHandleForProcess(processId);
            if (hWnd == IntPtr.Zero) return;
            ShowWindow(hWnd, SW_RESTORE);
            SetForegroundWindow(hWnd);
        }
    }
}
