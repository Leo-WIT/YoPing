using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Windows.Media;

namespace vmPing.Classes
{
    public static class ApplicationOptions
    {
        public enum PopupNotificationOption
        {
            Always,
            Never,
            WhenMinimized
        }

        public enum StartMode
        {
            Blank = 0,
            MultiInput = 1,
            Favorite = 2
        }

        public enum LatencyMode
        {
            Off,
            Auto,
            Fixed
        }

        public enum ThemeMode
        {
            Light,
            Dark
        }

        public enum TerminationMode
        {
            Continuous,
            Count,
            Duration
        }

        public enum PayloadSizeMode
        {
            Fixed,
            RandomRange
        }

        // Ping & probe options.
        public static int PingInterval { get; set; } = Constants.DefaultInterval;
        public static int PingTimeout { get; set; } = Constants.DefaultTimeout;
        public static TerminationMode StopMode { get; set; } = TerminationMode.Continuous;
        public static int PingCountLimit { get; set; } = 4;
        public static int RunDurationSeconds { get; set; } = 60;
        public static int MaxConcurrentTasks { get; set; } = 32;
        public static int AlertThreshold { get; set; } = 2;
        public static LatencyMode LatencyDetectionMode { get; set; } = LatencyMode.Off;
        public static long HighLatencyMilliseconds { get; set; } = 50;
        public static int HighLatencyAlertTiggerCount { get; set; } = 2;
        public static int TTL { get; set; } = Constants.DefaultTTL;
        public static bool DontFragment { get; set; } = false;
        public static bool ReverseResolveAddress { get; set; } = false;
        public static bool RecordRouteEnabled { get; set; } = false;
        public static int RecordRouteHopCount { get; set; } = 9;
        public static string SourceAddress { get; set; } = string.Empty;
        public static string LooseSourceRoute { get; set; } = string.Empty;
        public static bool UseCustomBuffer { get; set; } = false;
        public static byte[] Buffer { get; set; }
        public static PayloadSizeMode PayloadMode { get; set; } = PayloadSizeMode.Fixed;
        public static int FixedPayloadSize { get; set; } = 32;
        public static int RandomPayloadMin { get; set; } = 32;
        public static int RandomPayloadMax { get; set; } = 64;
        public const int MinimumPingIntervalMilliseconds = 200;
        public const int MaximumSafePayloadBytes = 1400;
        public static SemaphoreSlim PingConcurrencyGate { get; private set; } = new SemaphoreSlim(32, 32);
        private static readonly object RandomLock = new object();
        private static readonly System.Random Random = new System.Random();
        public static PingOptions PingOptions { get; }

        public static bool UseWindowsNativePing =>
            ReverseResolveAddress ||
            RecordRouteEnabled ||
            !string.IsNullOrWhiteSpace(SourceAddress) ||
            !string.IsNullOrWhiteSpace(LooseSourceRoute);

        // Popup notifications.
        public static PopupNotificationOption PopupOption { get; set; } = PopupNotificationOption.Always;
        public static bool IsAutoDismissEnabled { get; set; } = false;
        public static int AutoDismissMilliseconds { get; set; } = 7000;

        // Email notifications.
        public static bool IsEmailAlertEnabled { get; set; } = false;
        public static bool IsEmailAuthenticationRequired { get; set; } = false;
        public static bool IsEmailSslEnabled { get; set; } = false;
        public static string EmailServer { get; set; }
        public static string EmailUser { get; set; }
        public static string EmailPassword { get; set; }
        public static string EmailPort { get; set; } = "25";
        public static string EmailRecipient { get; set; }
        public static string EmailFromAddress { get; set; }

        // Audio alerts.
        public static bool IsAudioUpAlertEnabled { get; set; } = false;
        public static bool IsAudioDownAlertEnabled { get; set; } = false;
        public static string AudioUpFilePath { get; set; }
        public static string AudioDownFilePath { get; set; }

        // Logging.
        public static bool IsLogOutputEnabled { get; set; } = false;
        public static string LogPath { get; set; }
        public static bool IsLogStatusChangesEnabled { get; set; } = false;
        public static string LogStatusChangesPath { get; set; }
        
        // Startup options.
        public static StartMode InitialStartMode { get; set; } = StartMode.Blank;
        public static int InitialProbeCount { get; set; } = 2;
        public static int InitialColumnCount { get; set; } = 2;
        public static string InitialFavorite { get; set; } = null;

        // Display options.
        public static ThemeMode Theme { get; set; } = ThemeMode.Light;
        public static bool IsAlwaysOnTopEnabled { get; set; } = false;
        public static bool IsMinimizeToTrayEnabled { get; set; } = false;
        public static bool IsExitToTrayEnabled { get; set; } = false;

        // Fonts.
        public static int FontSize_Probe { get; set; } = 11;
        public static int FontSize_Scanner { get; set; } = 16;

        // Probe background colors.
        public static string BackgroundColor_Probe_Inactive { get; set; } = Constants.Color_Probe_Background_Inactive;
        public static string BackgroundColor_Probe_Up { get; set; } = Constants.Color_Probe_Background_Up;
        public static string BackgroundColor_Probe_Down { get; set; } = Constants.Color_Probe_Background_Down;
        public static string BackgroundColor_Probe_Indeterminate { get; set; } = Constants.Color_Probe_Background_Indeterminate;
        public static string BackgroundColor_Probe_Error { get; set; } = Constants.Color_Probe_Background_Error;
        public static string BackgroundColor_Probe_Scanner { get; set; } = Constants.Color_Probe_Background_Scanner;

        // Probe foreground colors.
        public static string ForegroundColor_Probe_Inactive { get; set; } = Constants.Color_Probe_Foreground_Inactive;
        public static string ForegroundColor_Probe_Up { get; set; } = Constants.Color_Probe_Foreground_Up;
        public static string ForegroundColor_Probe_Down { get; set; } = Constants.Color_Probe_Foreground_Down;
        public static string ForegroundColor_Probe_Indeterminate { get; set; } = Constants.Color_Probe_Foreground_Indeterminate;
        public static string ForegroundColor_Probe_Error { get; set; } = Constants.Color_Probe_Foreground_Error;
        public static string ForegroundColor_Probe_Scanner { get; set; } = Constants.Color_Probe_Foreground_Scanner;

        // Probe statistics.
        public static string ForegroundColor_Stats_Inactive { get; set; } = Constants.Color_Statistics_Foreground_Inactive;
        public static string ForegroundColor_Stats_Up { get; set; } = Constants.Color_Statistics_Foreground_Up;
        public static string ForegroundColor_Stats_Down { get; set; } = Constants.Color_Statistics_Foreground_Down;
        public static string ForegroundColor_Stats_Indeterminate { get; set; } = Constants.Color_Statistics_Foreground_Indeterminate;
        public static string ForegroundColor_Stats_Error { get; set; } = Constants.Color_Statistics_Foreground_Error;

        // Alias text.
        public static string ForegroundColor_Alias_Inactive { get; set; } = Constants.Color_Alias_Foreground_Inactive;
        public static string ForegroundColor_Alias_Up { get; set; } = Constants.Color_Alias_Foreground_Up;
        public static string ForegroundColor_Alias_Down { get; set; } = Constants.Color_Alias_Foreground_Down;
        public static string ForegroundColor_Alias_Indeterminate { get; set; } = Constants.Color_Alias_Foreground_Indeterminate;
        public static string ForegroundColor_Alias_Error { get; set; } = Constants.Color_Alias_Foreground_Error;
        public static string ForegroundColor_Alias_Scanner { get; set; } = Constants.Color_Alias_Foreground_Scanner;

        static ApplicationOptions()
        {
            // Set the default ping data.
            Buffer = Encoding.ASCII.GetBytes(Constants.DefaultIcmpData);
            LogPath = GetDefaultLogDirectory();
            LogStatusChangesPath = GetDefaultStatusLogPath();

            // Set the default ping options.
            PingOptions = new PingOptions(Constants.DefaultTTL, true);
        }

        public static string GetDefaultLogDirectory()
        {
            return Path.Combine(GetRealUserDocumentsDirectory(), "YoPing", "Logs");
        }

        public static string GetDefaultStatusLogPath()
        {
            return Path.Combine(GetDefaultLogDirectory(), $"yoping-status_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        }

        public static string NormalizeUserPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            string normalized = path.Replace('/', '\\').Trim();
            int driveIndex = normalized.IndexOf(@"\drive\", StringComparison.OrdinalIgnoreCase);
            if (driveIndex >= 0 && normalized.Length > driveIndex + 9)
            {
                int driveLetterIndex = driveIndex + @"\drive\".Length;
                char driveLetter = normalized[driveLetterIndex];
                int slashAfterDrive = driveLetterIndex + 1;
                if (char.IsLetter(driveLetter) &&
                    slashAfterDrive < normalized.Length &&
                    normalized[slashAfterDrive] == '\\')
                {
                    return char.ToUpperInvariant(driveLetter) + @":\" + normalized.Substring(slashAfterDrive + 1);
                }
            }

            return normalized;
        }

        public static string ToPhysicalIoPath(string path)
        {
            string normalized = NormalizeUserPath(path);
            if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith(@"\\?\"))
            {
                return normalized;
            }

            if (normalized.Length >= 3 &&
                char.IsLetter(normalized[0]) &&
                normalized[1] == ':' &&
                normalized[2] == '\\')
            {
                return @"\\?\" + normalized;
            }

            return normalized;
        }

        private static string GetRealUserDocumentsDirectory()
        {
            string documents = NormalizeUserPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (!string.IsNullOrWhiteSpace(documents) &&
                documents.IndexOf(@"\Documents", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return documents;
            }

            string userProfile = NormalizeUserPath(Environment.GetEnvironmentVariable("USERPROFILE"));
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                return Path.Combine(userProfile, "Documents");
            }

            return @"C:\Users\LL\Documents";
        }

        public static void UpdatePingOptions()
        {
            if (PingInterval < MinimumPingIntervalMilliseconds)
            {
                PingInterval = MinimumPingIntervalMilliseconds;
            }

            if (MaxConcurrentTasks < 1)
            {
                MaxConcurrentTasks = 1;
            }
            else if (MaxConcurrentTasks > 256)
            {
                MaxConcurrentTasks = 256;
            }

            PingConcurrencyGate = new SemaphoreSlim(MaxConcurrentTasks, MaxConcurrentTasks);
            PingOptions.Ttl = TTL;
            PingOptions.DontFragment = DontFragment;
        }

        public static byte[] CreatePayloadBuffer()
        {
            int size = FixedPayloadSize;
            if (PayloadMode == PayloadSizeMode.RandomRange)
            {
                int min = RandomPayloadMin;
                int max = RandomPayloadMax;
                if (min > max)
                {
                    int temp = min;
                    min = max;
                    max = temp;
                }
                min = ClampPayloadSize(min);
                max = ClampPayloadSize(max);
                lock (RandomLock)
                {
                    size = Random.Next(min, max + 1);
                }
            }
            else
            {
                size = ClampPayloadSize(size);
            }

            var buffer = new byte[size];
            if (size >= Constants.DefaultIcmpData.Length)
            {
                System.Buffer.BlockCopy(Encoding.ASCII.GetBytes(Constants.DefaultIcmpData), 0, buffer, 0, Constants.DefaultIcmpData.Length);
            }
            return buffer;
        }

        private static int ClampPayloadSize(int size)
        {
            if (size < 0)
            {
                return 0;
            }
            return size > MaximumSafePayloadBytes ? MaximumSafePayloadBytes : size;
        }

        public static IEnumerable<Visual> GetChildren(this Visual parent, bool recurse = true)
        {
            if (parent == null)
            {
                yield break;
            }

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                // Retrieve child visual at specified index value.
                if (VisualTreeHelper.GetChild(parent, i) is Visual child)
                {
                    yield return child;

                    if (recurse)
                    {
                        foreach (var grandChild in child.GetChildren(true))
                        {
                            yield return grandChild;
                        }
                    }
                }
            }
        }
    }
}
