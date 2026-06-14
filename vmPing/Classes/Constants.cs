using System.Windows.Input;

namespace vmPing.Classes
{
    class Constants
    {
        // Default probe background colors.
        public const string Color_Probe_Background_Inactive = "#F8FAFC";
        public const string Color_Probe_Background_Up = "#ECFDF5";
        public const string Color_Probe_Background_Down = "#FEF2F2";
        public const string Color_Probe_Background_Indeterminate = "#FFFBEB";
        public const string Color_Probe_Background_Error = "#FFF7ED";
        public const string Color_Probe_Background_Scanner = "#EFF6FF";

        // Default probe foreground colors.
        public const string Color_Probe_Foreground_Inactive = "#334155";
        public const string Color_Probe_Foreground_Up = "#065F46";
        public const string Color_Probe_Foreground_Down = "#991B1B";
        public const string Color_Probe_Foreground_Indeterminate = "#92400E";
        public const string Color_Probe_Foreground_Error = "#9A3412";
        public const string Color_Probe_Foreground_Scanner = "#1E40AF";

        // Default statistics foreground colors.
        public const string Color_Statistics_Foreground_Inactive = "#475569";
        public const string Color_Statistics_Foreground_Up = "#047857";
        public const string Color_Statistics_Foreground_Down = "#B91C1C";
        public const string Color_Statistics_Foreground_Indeterminate = "#B45309";
        public const string Color_Statistics_Foreground_Error = "#C2410C";

        // Default alias / probe title colors.
        public const string Color_Alias_Foreground_Inactive = "#0F172A";
        public const string Color_Alias_Foreground_Up = "#064E3B";
        public const string Color_Alias_Foreground_Down = "#7F1D1D";
        public const string Color_Alias_Foreground_Indeterminate = "#78350F";
        public const string Color_Alias_Foreground_Error = "#7C2D12";
        public const string Color_Alias_Foreground_Scanner = "#1E3A8A";

        // Default probe options.
        public const string DefaultIcmpData = "https://github.com/Leo-WIT/YoPing";
        public const int DefaultTimeout = 2000;       // In miliseconds.
        public const int DefaultTTL = 64;
        public const int DefaultInterval = 1000;      // In miliseconds.

        // Default audio alert file paths.
        public const string DefaultAudioDownFilePath = @"%WINDIR%\Media\Windows Notify Email.wav";
        public const string DefaultAudioUpFilePath = @"%WINDIR%\Media\Windows Unlock.wav";

        // Key bindings.
        public const Key StatusHistoryKeyBinding = Key.F12;
        public const Key HelpKeyBinding = Key.F1;
    }
}
