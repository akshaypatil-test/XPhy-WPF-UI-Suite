#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
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

        // Video calling applications (from Media Applications list)
        // Note: msedge.exe is NOT listed here so Edge is only detected as a browser (YouTube, etc.). Teams Chat uses msedgewebview2.exe.
        private static readonly HashSet<string> VideoCallingProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Zoom.exe",
            "CptHost.exe", // Zoom
            "ms-teams.exe",
            "Teams.exe",
            "Meet.exe", // Google Meet standalone
            "msedgewebview2.exe", // Teams Chat (embedded Edge)
            "webex.exe", // Cisco Webex
            "Skype.exe",
            "SkypeApp.exe",
            "SkypeHost.exe",
            "slack.exe",
            "discord.exe",
            "bluejeans.exe",
            "g2mcomm.exe", // GoTo Meeting
            "jiomeet.exe"  // JioMeet
        };

        // Media players (from Media Applications list)
        private static readonly HashSet<string> MediaPlayerProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "vlc.exe",
            "wmplayer.exe",
            "wmpshare.exe",
            "MediaPlayer.exe",
            "mplayer2.exe",   // Windows Media Player Legacy
            "quicktimeplayer.exe",
            "kmplayer.exe",
            "gom.exe",        // GOM Player
            "potplayer.exe",
            "mxplayer.exe",
            "netflix.exe",    // Netflix app
            "microsoft.media.player.exe"  // Windows 11 Media Player (UWP)
        };

        // Virtual camera / streaming (from Media Applications list)
        private static readonly HashSet<string> StreamingProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "obs64.exe",           // OBS Studio
            "streamlabs.exe",      // Streamlabs
            "xsplit.core.exe",     // XSplit Broadcaster
            "manycam.exe",
            "snapcamera.exe",      // Snap Camera
            "nvidia broadcast.exe" // NVIDIA Broadcast
        };

        // Browsers (for YouTube/streaming detection)
        private static readonly HashSet<string> BrowserProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "chrome.exe",
            "msedge.exe",
            "firefox.exe",
            "opera.exe",
            "brave.exe"
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
                        // msedgewebview2.exe is used by Teams Chat; only add when window title indicates Teams
                        if (processName.Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!processWindows.TryGetValue((uint)process.Id, out var edgeTitles) || edgeTitles.Count == 0)
                                continue;
                            bool isTeamsChat = edgeTitles.Any(title =>
                                title.IndexOf("Teams", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                title.IndexOf("Microsoft Teams", StringComparison.OrdinalIgnoreCase) >= 0);
                            if (!isTeamsChat)
                                continue;
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
                    // Check for virtual camera / streaming apps
                    else if (StreamingProcesses.Contains(processName))
                    {
                        string displayName = GetDisplayName(processName);
                        detected = new DetectedProcess
                        {
                            ProcessId = process.Id,
                            ProcessName = processName,
                            DisplayName = displayName,
                            ProcessType = "Streaming",
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

                        // Chrome only: add Google Chat as VideoCalling when window title indicates Chat (PWA), not as "Google Chrome"
                        if (processName.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var browserProcess in allBrowserProcesses)
                            {
                                if (addedProcessIds.Contains(browserProcess.Id)) continue;
                                if (!processWindows.TryGetValue((uint)browserProcess.Id, out var titles) || titles.Count == 0) continue;
                                if (!IsGoogleChatWindow(titles)) continue;
                                var chatDetected = new DetectedProcess
                                {
                                    ProcessId = browserProcess.Id,
                                    ProcessName = processName,
                                    DisplayName = "Google Chat",
                                    ProcessType = "VideoCalling",
                                    HasYouTubeTab = false,
                                    WindowTitle = titles[0]
                                };
                                addedProcessIds.Add(browserProcess.Id);
                                detectedProcesses.Add(chatDetected);
                            }
                        }

                        // YouTube is present if it appears in any tab (active or inactive). We use window titles from EnumWindows
                        // plus UI Automation to read all tab titles from each browser window, so background tabs are detected.
                        DetectedProcess? browserDetected = null;
                        foreach (var browserProcess in allBrowserProcesses)
                        {
                            var allTitles = new List<string>();
                            if (processWindows.TryGetValue((uint)browserProcess.Id, out var windowTitles))
                                allTitles.AddRange(windowTitles);
                            foreach (IntPtr hWnd in GetAllWindowHandlesForProcess(browserProcess.Id))
                            {
                                foreach (var tabTitle in GetTabTitlesFromBrowserWindow(hWnd))
                                {
                                    if (!string.IsNullOrWhiteSpace(tabTitle) && !allTitles.Contains(tabTitle))
                                        allTitles.Add(tabTitle);
                                }
                            }
                            if (allTitles.Count > 0 && HasYouTubeTab(allTitles))
                            {
                                string displayName = GetDisplayName(processName);
                                browserDetected = new DetectedProcess
                                {
                                    ProcessId = browserProcess.Id,
                                    ProcessName = processName,
                                    DisplayName = displayName,
                                    ProcessType = "Browser",
                                    HasYouTubeTab = true,
                                    WindowTitle = GetYouTubeWindowTitle(allTitles)
                                };
                                break;
                            }
                        }

                        // Only add browser when it has YouTube; add directly so we get both Google Chat and Google Chrome when same process has both
                        if (browserDetected != null)
                        {
                            detectedProcesses.Add(browserDetected);
                            detected = null; // already added above; don't add again in outer block
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

            // Fallback: detect apps by window title when process name is not in our list (e.g. Task Manager shows no process name)
            // This handles UWP apps (ApplicationFrameHost.exe) and grouped apps where the visible window is under a host process
            foreach (var kv in processWindows)
            {
                uint processId = kv.Key;
                var windowTitles = kv.Value;
                if (windowTitles == null || windowTitles.Count == 0) continue;
                if (addedProcessIds.Contains((int)processId)) continue;

                try
                {
                    using var proc = Process.GetProcessById((int)processId);
                    string procName = proc.ProcessName + ".exe";
                    bool isKnown = VideoCallingProcesses.Contains(procName) || MediaPlayerProcesses.Contains(procName)
                        || StreamingProcesses.Contains(procName) || BrowserProcesses.Contains(procName);
                    if (isKnown) continue;

                    // UWP / host processes that often have no process name shown in Task Manager
                    bool isHostProcess = procName.Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase)
                        || procName.Equals("RuntimeBroker.exe", StringComparison.OrdinalIgnoreCase)
                        || procName.Equals("SearchApp.exe", StringComparison.OrdinalIgnoreCase);

                    if (!isHostProcess) continue;

                    var match = TryMatchAppByWindowTitle(windowTitles);
                    if (match == null) continue;

                    var fallbackDetected = new DetectedProcess
                    {
                        ProcessId = (int)processId,
                        ProcessName = procName,
                        DisplayName = match.Value.DisplayName,
                        ProcessType = match.Value.ProcessType,
                        HasYouTubeTab = match.Value.ProcessType == "Browser" && windowTitles.Any(t =>
                            t.IndexOf("YouTube", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            t.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0),
                        WindowTitle = windowTitles[0]
                    };
                    addedProcessIds.Add((int)processId);
                    detectedProcesses.Add(fallbackDetected);
                }
                catch
                {
                    // Process may have exited
                }
            }

            // Remove duplicates by DisplayName (in case same app appears with different ProcessIds)
            // Group by DisplayName and take the first occurrence of each
            var uniqueByDisplayName = detectedProcesses
                .GroupBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            // Show all browsers that have YouTube (e.g. both Chrome and Edge when both have YouTube)
            var browsers = uniqueByDisplayName.Where(p => p.ProcessType == "Browser").ToList();
            var nonBrowsers = uniqueByDisplayName.Where(p => p.ProcessType != "Browser").ToList();
            var selectedBrowsers = browsers;

            // Combine non-browsers with all detected browsers (call apps, media players, streaming, browsers with YouTube)
            var combined = nonBrowsers.Concat(selectedBrowsers).ToList();

            // Choose only 3 processes: those with highest memory usage (working set)
            var sorted = combined
                .OrderByDescending(p => GetProcessWorkingSetBytes(p.ProcessId))
                .Take(3)
                .ToList();

            return sorted;
        }

        /// <summary>Match app by window title for UWP/host processes (e.g. ApplicationFrameHost) where Task Manager shows no process name.</summary>
        private static (string DisplayName, string ProcessType)? TryMatchAppByWindowTitle(List<string> windowTitles)
        {
            bool hasYouTube = windowTitles.Any(t =>
                (t != null && (t.IndexOf("YouTube", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0)));
            foreach (var title in windowTitles)
            {
                if (string.IsNullOrWhiteSpace(title)) continue;
                string t = title.Trim();

                if (t.IndexOf("Media Player", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("Media Player", "MediaPlayer");
                if (t.IndexOf("Microsoft Edge", StringComparison.OrdinalIgnoreCase) >= 0 && hasYouTube)
                    return ("Microsoft Edge", "Browser");
                // Google Chat (PWA/app) must be checked before "Google Chrome" so it's not misidentified
                if (t.IndexOf("Google Chat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (t.IndexOf("Chat", StringComparison.OrdinalIgnoreCase) >= 0 && t.IndexOf("chat.google.com", StringComparison.OrdinalIgnoreCase) >= 0))
                    return ("Google Chat", "VideoCalling");
                if (t.IndexOf("Google Chrome", StringComparison.OrdinalIgnoreCase) >= 0 && hasYouTube)
                    return ("Google Chrome", "Browser");
                if ((t.IndexOf("Mozilla Firefox", StringComparison.OrdinalIgnoreCase) >= 0 || (t.IndexOf("Firefox", StringComparison.OrdinalIgnoreCase) >= 0 && t.Length < 50)) && hasYouTube)
                    return ("Mozilla Firefox", "Browser");
                if (t.IndexOf("Opera", StringComparison.OrdinalIgnoreCase) >= 0 && hasYouTube)
                    return ("Opera", "Browser");
                if (t.IndexOf("Brave", StringComparison.OrdinalIgnoreCase) >= 0 && hasYouTube)
                    return ("Brave", "Browser");
                if (t.IndexOf("Microsoft Teams", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Teams", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("Microsoft Teams", "VideoCalling");
                if (t.IndexOf("VLC", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("VLC Media Player", "MediaPlayer");
                if (t.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("Zoom", "VideoCalling");
                if (t.IndexOf("Webex", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("Cisco Webex", "VideoCalling");
                if (t.IndexOf("Slack", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("Slack", "VideoCalling");
                if (t.IndexOf("Discord", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("Discord", "VideoCalling");
                if (t.IndexOf("Skype", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("Skype", "VideoCalling");
                if (t.IndexOf("Windows Media Player", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("Windows Media Player", "MediaPlayer");
            }
            return null;
        }

        private string GetDisplayName(string processName)
        {
            return processName.ToLower() switch
            {
                "zoom.exe" or "cpthost.exe" => "Zoom",
                "ms-teams.exe" or "teams.exe" => "Microsoft Teams",
                "msedgewebview2.exe" => "Microsoft Teams Chat",
                "skype.exe" or "skypeapp.exe" or "skypehost.exe" => "Skype",
                "meet.exe" => "Google Meet",
                "webex.exe" => "Cisco Webex",
                "slack.exe" => "Slack",
                "discord.exe" => "Discord",
                "bluejeans.exe" => "BlueJeans",
                "g2mcomm.exe" => "GoTo Meeting",
                "jiomeet.exe" => "JioMeet",
                "vlc.exe" => "VLC Media Player",
                "wmplayer.exe" or "wmpshare.exe" or "mediaplayer.exe" => "Windows Media Player",
                "mplayer2.exe" => "Windows Media Player Legacy",
                "quicktimeplayer.exe" => "QuickTime Player",
                "kmplayer.exe" => "KMPlayer",
                "gom.exe" => "GOM Player",
                "potplayer.exe" => "PotPlayer",
                "mxplayer.exe" => "MX Player",
                "netflix.exe" => "Netflix",
                "microsoft.media.player.exe" => "Media Player",
                "obs64.exe" => "OBS Studio",
                "streamlabs.exe" => "Streamlabs",
                "xsplit.core.exe" => "XSplit Broadcaster",
                "manycam.exe" => "ManyCam",
                "snapcamera.exe" => "Snap Camera",
                "nvidia broadcast.exe" => "NVIDIA Broadcast",
                "chrome.exe" => "Google Chrome",
                "msedge.exe" => "Microsoft Edge",
                "firefox.exe" => "Mozilla Firefox",
                "opera.exe" => "Opera",
                "brave.exe" => "Brave",
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

        /// <summary>True if the window is Google Chat (PWA or app), not generic Chrome.</summary>
        private static bool IsGoogleChatWindow(List<string> windowTitles)
        {
            foreach (var title in windowTitles)
            {
                if (string.IsNullOrWhiteSpace(title)) continue;
                if (title.IndexOf("Google Chat", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (title.IndexOf("chat.google.com", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>True if YouTube appears in any of the given titles. For browsers we pass window titles plus all tab titles
        /// from UI Automation (active and inactive tabs), so YouTube is detected when present in any tab.</summary>
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

        /// <summary>Gets the working set (physical memory) of the process in bytes. Returns 0 if process is not found or inaccessible.</summary>
        private static long GetProcessWorkingSetBytes(int processId)
        {
            try
            {
                using (var proc = Process.GetProcessById(processId))
                    return proc.WorkingSet64;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets all top-level window handles for a process (visible or minimized, with a title). Used to read tab titles via UI Automation.
        /// </summary>
        private static List<IntPtr> GetAllWindowHandlesForProcess(int processId)
        {
            var list = new List<IntPtr>();
            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if ((int)pid == processId && IsWindow(hWnd) && (IsWindowVisible(hWnd) || IsIconic(hWnd)))
                {
                    if (GetWindowTextLength(hWnd) > 0)
                        list.Add(hWnd);
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }

        /// <summary>
        /// Gets titles of all tabs in a browser window via UI Automation (active and inactive). Returns empty list if UIA fails (e.g. minimized).
        /// </summary>
        private static List<string> GetTabTitlesFromBrowserWindow(IntPtr hWnd)
        {
            var titles = new List<string>();
            try
            {
                var root = AutomationElement.FromHandle(hWnd);
                if (root == null) return titles;
                var cond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem);
                var tabItems = root.FindAll(TreeScope.Descendants, cond);
                if (tabItems == null) return titles;
                for (int i = 0; i < tabItems.Count; i++)
                {
                    try
                    {
                        var name = tabItems[i].Current.Name;
                        if (!string.IsNullOrWhiteSpace(name))
                            titles.Add(name);
                    }
                    catch { /* skip inaccessible tab */ }
                }
            }
            catch { /* UIA can fail for minimized or protected windows */ }
            return titles;
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

        /// <summary>
        /// Finds a top-level window whose title contains the given substring (case-insensitive).
        /// Used for UWP apps (e.g. Windows 11 Media Player) where the process may have MainWindowHandle zero.
        /// </summary>
        public static IntPtr FindWindowByTitleContains(string titleSubstring)
        {
            if (string.IsNullOrWhiteSpace(titleSubstring)) return IntPtr.Zero;
            IntPtr found = IntPtr.Zero;
            string lower = titleSubstring.Trim().ToLowerInvariant();
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindow(hWnd) || (!IsWindowVisible(hWnd) && !IsIconic(hWnd)))
                    return true;
                int len = GetWindowTextLength(hWnd);
                if (len <= 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (sb.ToString().Trim().ToLowerInvariant().IndexOf(lower, StringComparison.Ordinal) >= 0)
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        /// <summary>
        /// Activates or launches the Windows 11 Media Player (UWP). Uses shell AUMID so that if the app
        /// is already running (including suspended), Windows brings it to the foreground and resumes it.
        /// </summary>
        public static bool TryActivateOrLaunchMediaPlayer(string processName)
        {
            if (!processName.Equals("Microsoft.Media.Player.exe", StringComparison.OrdinalIgnoreCase))
                return false;
            try
            {
                // Shell activation with AUMID: launches if not running, activates (resumes from suspended) and brings to foreground if already running
                var startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "shell:AppsFolder\\Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                Process.Start(startInfo);
                return true;
            }
            catch
            {
                try
                {
                    string aliasPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    aliasPath = System.IO.Path.Combine(aliasPath, "Microsoft", "WindowsApps", "Microsoft.Media.Player.exe");
                    if (System.IO.File.Exists(aliasPath))
                    {
                        Process.Start(new ProcessStartInfo { FileName = aliasPath, UseShellExecute = true });
                        return true;
                    }
                }
                catch { }
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = "wmplayer.exe", UseShellExecute = true });
                    return true;
                }
                catch { }
            }
            return false;
        }

        /// <summary>
        /// Ensures the selected media player app is running and brings its window to the foreground.
        /// For Windows 11 Media Player (UWP), always activates via AUMID so a suspended or minimized app is resumed and shown.
        /// </summary>
        public static void EnsureMediaPlayerOpenAndForeground(DetectedProcess selectedProcess)
        {
            if (selectedProcess == null) return;
            bool isWin11MediaPlayer = selectedProcess.ProcessName.Equals("Microsoft.Media.Player.exe", StringComparison.OrdinalIgnoreCase)
                || (selectedProcess.DisplayName?.IndexOf("Media Player", StringComparison.OrdinalIgnoreCase) >= 0 && selectedProcess.ProcessType == "MediaPlayer");

            if (isWin11MediaPlayer)
            {
                // Always activate/launch via AUMID so that suspended or minimized UWP app is resumed and brought to foreground
                TryActivateOrLaunchMediaPlayer(selectedProcess.ProcessName);
                System.Threading.Thread.Sleep(2800);
            }

            IntPtr hWnd = GetWindowHandleForProcess(selectedProcess.ProcessId);
            if (hWnd == IntPtr.Zero && isWin11MediaPlayer)
                hWnd = FindWindowByTitleContains("Media Player");
            if (hWnd == IntPtr.Zero)
                hWnd = GetWindowHandleForProcess(selectedProcess.ProcessId);
            if (hWnd == IntPtr.Zero && isWin11MediaPlayer)
                hWnd = FindWindowByTitleContains("Media Player");
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
        }
    }
}
