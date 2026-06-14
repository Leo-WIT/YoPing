using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using vmPing.Classes;

namespace vmPing.UI
{
    public partial class OptionsWindow : Window
    {
        private class SourceInterfaceOption
        {
            public string Name { get; set; }
            public string Address { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }

        // Imports and constants for hiding minimize and maximize buttons.
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x10000; //maximize button
        private const int WS_MINIMIZEBOX = 0x20000; //minimize button

        public OptionsWindow()
        {
            InitializeComponent();

            PopulateGeneralOptions();
            PopulateNotificationOptions();
            PopulateAudioAlertOptions();
            PopulateLogOutputOptions();
            PopulateAdvancedOptions();
            PopulateDisplayOptions();
            PopulateLayoutOptions();
        }

        private bool? ShowError(string message, TabItem tabItem, Control control, bool isWarning = false)
        {
            // Switch to specified tab.
            tabItem?.Focus();

            // Show warning or error?
            DialogWindow errorWindow;
            if (isWarning == true)
            {
                errorWindow = DialogWindow.WarningWindow(message, "Save");
            }
            else
            {
                errorWindow = DialogWindow.ErrorWindow(message);
            }

            // Display dialog and capture result.
            errorWindow.Owner = this;
            var result = errorWindow.ShowDialog();

            // Set focus to specified control.
            control?.Focus();

            return result;
        }

        private void PopulateGeneralOptions()
        {
            int pingInterval = ApplicationOptions.PingInterval;
            int pingTimeout = ApplicationOptions.PingTimeout;

            pingTimeout /= 1000;

            PingInterval.Text = pingInterval.ToString();
            PingTimeout.Text = pingTimeout.ToString();
            AlertThreshold.Text = ApplicationOptions.AlertThreshold.ToString();
            PingIntervalUnits.Visibility = Visibility.Collapsed;
            StopNeverOption.IsChecked = ApplicationOptions.StopMode == ApplicationOptions.TerminationMode.Continuous;
            StopByCountOption.IsChecked = ApplicationOptions.StopMode == ApplicationOptions.TerminationMode.Count;
            StopByDurationOption.IsChecked = ApplicationOptions.StopMode == ApplicationOptions.TerminationMode.Duration;
            PingCountLimit.Text = ApplicationOptions.PingCountLimit.ToString();
            RunDurationSeconds.Text = ApplicationOptions.RunDurationSeconds.ToString();
            MaxConcurrentTasks.Text = ApplicationOptions.MaxConcurrentTasks.ToString();

            // Get startup mode settings.
            InitialProbeCount.Text = ApplicationOptions.InitialProbeCount.ToString();
            InitialColumnCount.Text = ApplicationOptions.InitialColumnCount.ToString();
            StartupMode.SelectedIndex = (int)ApplicationOptions.InitialStartMode;
            InitialFavorite.ItemsSource = Favorite.GetTitles();
            InitialFavorite.Text = ApplicationOptions.InitialFavorite ?? string.Empty;
        }

        private void PopulateNotificationOptions()
        {
            PopupsDisabledOption.IsChecked = false;
            PopupsMinimizedOption.IsChecked = false;
            PopupsAlwaysOption.IsChecked = false;
            switch (ApplicationOptions.PopupOption)
            {
                case ApplicationOptions.PopupNotificationOption.Never:
                    PopupsDisabledOption.IsChecked = true;
                    break;
                case ApplicationOptions.PopupNotificationOption.WhenMinimized:
                    PopupsMinimizedOption.IsChecked = true;
                    break;
                case ApplicationOptions.PopupNotificationOption.Always:
                    PopupsAlwaysOption.IsChecked = true;
                    break;
            }
            IsAutoDismissEnabled.IsChecked = ApplicationOptions.IsAutoDismissEnabled;
            AutoDismissInterval.Text = (ApplicationOptions.AutoDismissMilliseconds / 1000).ToString();
        }

        private void PopulateAudioAlertOptions()
        {
            IsAudioDownAlertEnabled.IsChecked = ApplicationOptions.IsAudioDownAlertEnabled;
            AudioDownFilePath.Text = ApplicationOptions.AudioDownFilePath;
            IsAudioUpAlertEnabled.IsChecked = ApplicationOptions.IsAudioUpAlertEnabled;
            AudioUpFilePath.Text = ApplicationOptions.AudioUpFilePath;
        }

        private void PopulateLogOutputOptions()
        {
            LogPath.Text = string.IsNullOrWhiteSpace(ApplicationOptions.LogPath)
                ? ApplicationOptions.GetDefaultLogDirectory()
                : ApplicationOptions.NormalizeUserPath(ApplicationOptions.LogPath);
            IsLogOutputEnabled.IsChecked = ApplicationOptions.IsLogOutputEnabled;
            LogStatusChangesPath.Text = string.IsNullOrWhiteSpace(ApplicationOptions.LogStatusChangesPath)
                ? ApplicationOptions.GetDefaultStatusLogPath()
                : ApplicationOptions.NormalizeUserPath(ApplicationOptions.LogStatusChangesPath);
            IsLogStatusChangesEnabled.IsChecked = ApplicationOptions.IsLogStatusChangesEnabled;
        }

        private void PopulateAdvancedOptions()
        {
            TTL.Text = ApplicationOptions.TTL.ToString();
            DontFragment.IsChecked = ApplicationOptions.DontFragment;
            ReverseResolveAddress.IsChecked = ApplicationOptions.ReverseResolveAddress;
            RecordRouteEnabled.IsChecked = ApplicationOptions.RecordRouteEnabled;
            RecordRouteHopCount.Text = ApplicationOptions.RecordRouteHopCount.ToString();
            SourceAddress.Text = ApplicationOptions.SourceAddress;
            LooseSourceRoute.Text = ApplicationOptions.LooseSourceRoute;
            PopulateSourceInterfaces();

            if (ApplicationOptions.PayloadMode == ApplicationOptions.PayloadSizeMode.RandomRange)
            {
                RandomPacketSizeOption.IsChecked = true;
                RandomPacketMin.Text = ApplicationOptions.RandomPayloadMin.ToString();
                RandomPacketMax.Text = ApplicationOptions.RandomPayloadMax.ToString();
            }
            else if (ApplicationOptions.UseCustomBuffer)
            {
                UseCustomPacketOption.IsChecked = true;
                PacketData.Text = Encoding.ASCII.GetString(ApplicationOptions.Buffer);
            }
            else
            {
                PacketSizeOption.IsChecked = true;
                PacketSize.Text = ApplicationOptions.FixedPayloadSize.ToString();
            }

            UpdateByteCount();
        }

        private void PopulateSourceInterfaces()
        {
            SourceInterface.Items.Clear();
            SourceInterface.Items.Add(new SourceInterfaceOption
            {
                Name = "不指定出口网卡",
                Address = string.Empty
            });

            foreach (var item in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                     n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                     n.Supports(NetworkInterfaceComponent.IPv4))
                         .SelectMany(n => n.GetIPProperties().UnicastAddresses
                             .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                             .Select(a => new SourceInterfaceOption
                             {
                                 Name = $"{n.Name} - {a.Address}（{n.Description}）",
                                 Address = a.Address.ToString()
                             }))
                         .OrderBy(x => x.Name))
            {
                SourceInterface.Items.Add(item);
            }

            var selected = SourceInterface.Items
                .OfType<SourceInterfaceOption>()
                .FirstOrDefault(x => x.Address == ApplicationOptions.SourceAddress);
            SourceInterface.SelectedItem = selected ?? SourceInterface.Items[0];
        }

        private void PopulateDisplayOptions()
        {
            LightThemeOption.IsChecked = ApplicationOptions.Theme == ApplicationOptions.ThemeMode.Light;
            DarkThemeOption.IsChecked = ApplicationOptions.Theme == ApplicationOptions.ThemeMode.Dark;
            IsAlwaysOnTopEnabled.IsChecked = ApplicationOptions.IsAlwaysOnTopEnabled;
            IsMinimizeToTrayEnabled.IsChecked = ApplicationOptions.IsMinimizeToTrayEnabled;
            IsExitToTrayEnabled.IsChecked = ApplicationOptions.IsExitToTrayEnabled;
        }

        private void PopulateLayoutOptions()
        {
            BackgroundColor_Probe_Inactive.Text = ApplicationOptions.BackgroundColor_Probe_Inactive;
            BackgroundColor_Probe_Up.Text = ApplicationOptions.BackgroundColor_Probe_Up;
            BackgroundColor_Probe_Down.Text = ApplicationOptions.BackgroundColor_Probe_Down;
            BackgroundColor_Probe_Error.Text = ApplicationOptions.BackgroundColor_Probe_Error;
            BackgroundColor_Probe_Indeterminate.Text = ApplicationOptions.BackgroundColor_Probe_Indeterminate;
            ForegroundColor_Probe_Inactive.Text = ApplicationOptions.ForegroundColor_Probe_Inactive;
            ForegroundColor_Probe_Up.Text = ApplicationOptions.ForegroundColor_Probe_Up;
            ForegroundColor_Probe_Down.Text = ApplicationOptions.ForegroundColor_Probe_Down;
            ForegroundColor_Probe_Error.Text = ApplicationOptions.ForegroundColor_Probe_Error;
            ForegroundColor_Probe_Indeterminate.Text = ApplicationOptions.ForegroundColor_Probe_Indeterminate;
            ForegroundColor_Stats_Inactive.Text = ApplicationOptions.ForegroundColor_Stats_Inactive;
            ForegroundColor_Stats_Up.Text = ApplicationOptions.ForegroundColor_Stats_Up;
            ForegroundColor_Stats_Down.Text = ApplicationOptions.ForegroundColor_Stats_Down;
            ForegroundColor_Stats_Error.Text = ApplicationOptions.ForegroundColor_Stats_Error;
            ForegroundColor_Stats_Indeterminate.Text = ApplicationOptions.ForegroundColor_Stats_Inactive;
            ForegroundColor_Alias_Inactive.Text = ApplicationOptions.ForegroundColor_Alias_Inactive;
            ForegroundColor_Alias_Up.Text = ApplicationOptions.ForegroundColor_Alias_Up;
            ForegroundColor_Alias_Down.Text = ApplicationOptions.ForegroundColor_Alias_Down;
            ForegroundColor_Alias_Error.Text = ApplicationOptions.ForegroundColor_Alias_Error;
            ForegroundColor_Alias_Indeterminate.Text = ApplicationOptions.ForegroundColor_Alias_Indeterminate;
        }


        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (SaveGeneralOptions() == false) return;
            if (SaveNotificationOptions() == false) return;
            if (SaveAudioAlertOptions() == false) return;
            if (SaveLogOutputOptions() == false) return;
            if (SaveAdvancedOptions() == false) return;
            if (SaveLayoutOptions() == false) return;
            if (SaveDisplayOptions() == false) return;

            if (SaveAsDefaults.IsChecked == true)
            {
                Configuration.Save();
            }

            DialogResult = true;
        }

        private bool SaveGeneralOptions()
        {
            if (PingInterval.Text.Length == 0)
            {
                ShowError("请输入有效的发包间隔。", GeneralTab, PingInterval);
                return false;
            }
            else if (PingTimeout.Text.Length == 0)
            {
                ShowError("请输入有效的 Ping 超时时间。", GeneralTab, PingTimeout);
                return false;
            }
            else if (AlertThreshold.Text.Length == 0)
            {
                ShowError("请输入有效的提醒阈值。", GeneralTab, AlertThreshold);
                return false;
            }

            // Ping interval in milliseconds.
            int pingInterval;
            if (!int.TryParse(PingInterval.Text, out pingInterval) ||
                pingInterval < ApplicationOptions.MinimumPingIntervalMilliseconds ||
                pingInterval > 86400000)
            {
                ShowError($"发包间隔必须在 {ApplicationOptions.MinimumPingIntervalMilliseconds} 到 86400000 毫秒之间。", GeneralTab, PingInterval);
                return false;
            }
            ApplicationOptions.PingInterval = pingInterval;

            int pingCountLimit;
            if (!int.TryParse(PingCountLimit.Text, out pingCountLimit) || pingCountLimit < 1 || pingCountLimit > 1000000)
            {
                ShowError("Ping 次数必须在 1 到 1000000 之间。", GeneralTab, PingCountLimit);
                return false;
            }
            ApplicationOptions.PingCountLimit = pingCountLimit;

            int runDurationSeconds;
            if (!int.TryParse(RunDurationSeconds.Text, out runDurationSeconds) || runDurationSeconds < 1 || runDurationSeconds > 86400)
            {
                ShowError("运行时长必须在 1 到 86400 秒之间。", GeneralTab, RunDurationSeconds);
                return false;
            }
            ApplicationOptions.RunDurationSeconds = runDurationSeconds;
            ApplicationOptions.StopMode = StopNeverOption.IsChecked == true
                ? ApplicationOptions.TerminationMode.Continuous
                : StopByDurationOption.IsChecked == true
                    ? ApplicationOptions.TerminationMode.Duration
                    : ApplicationOptions.TerminationMode.Count;

            int maxConcurrentTasks;
            if (!int.TryParse(MaxConcurrentTasks.Text, out maxConcurrentTasks) || maxConcurrentTasks < 1 || maxConcurrentTasks > 256)
            {
                ShowError("最大并发必须在 1 到 256 之间。", GeneralTab, MaxConcurrentTasks);
                return false;
            }
            ApplicationOptions.MaxConcurrentTasks = maxConcurrentTasks;

            // Ping timeout.
            int pingTimeout;
            if (int.TryParse(PingTimeout.Text, out pingTimeout) && pingTimeout > 0 && pingTimeout <= 60)
            {
                pingTimeout *= 1000;
            }
            else
            {
                pingTimeout = Constants.DefaultTimeout;
            }
            ApplicationOptions.PingTimeout = pingTimeout;

            // Alert threshold.
            int alertThreshold;

            var isThresholdValid = int.TryParse(AlertThreshold.Text, out alertThreshold) && alertThreshold > 0 && alertThreshold <= 60;
            if (!isThresholdValid)
            {
                alertThreshold = 1;
            }

            ApplicationOptions.AlertThreshold = alertThreshold;

            // Startup mode.
            ApplicationOptions.InitialStartMode = (ApplicationOptions.StartMode)StartupMode.SelectedIndex;
            switch (StartupMode.SelectedIndex)
            {
                case ((int)ApplicationOptions.StartMode.Blank):
                case ((int)ApplicationOptions.StartMode.MultiInput):
                    // Initial probe count.
                    int count;
                    if (int.TryParse(InitialProbeCount.Text, out count))
                    {
                        if (count < 1)
                        {
                            count = 1;
                        }
                        else if (count > 20)
                        {
                            count = 2;
                        }
                    }
                    else
                    {
                        count = 2;
                    }
                    ApplicationOptions.InitialProbeCount = count;

                    // Initial column count.
                    if (int.TryParse(InitialColumnCount.Text, out count))
                    {
                        if (count < 1)
                        {
                            count = 1;
                        }
                        else if (count > 10)
                        {
                            count = 10;
                        }
                    }
                    else
                    {
                        count = 2;
                    }
                    ApplicationOptions.InitialColumnCount = count;
                    break;
                case ((int)ApplicationOptions.StartMode.Favorite):
                    // Initial favorite.
                    ApplicationOptions.InitialFavorite = InitialFavorite.Text;
                    break;
            }

            return true;
        }

        private bool SaveNotificationOptions()
        {
            if (IsAutoDismissEnabled.IsChecked == true)
            {
                if (int.TryParse(AutoDismissInterval.Text, out int result) && result > 0 && result < 100)
                {
                    ApplicationOptions.AutoDismissMilliseconds = result * 1000;
                    ApplicationOptions.IsAutoDismissEnabled = true;
                }
                else
                {
                    ShowError("请输入有效的自动关闭秒数。", PopupAlertsTab, AutoDismissInterval);
                    return false;
                }
            }
            else
            {
                ApplicationOptions.IsAutoDismissEnabled = false;
            }

            if (PopupsMinimizedOption.IsChecked == true)
            {
                ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.WhenMinimized;
            }
            else if (PopupsAlwaysOption.IsChecked == true)
            {
                ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.Always;
            }
            else
            {
                ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.Never;
            }

            return true;
        }

        private bool SaveAdvancedOptions()
        {
            // Validate input.

            var regex = new Regex("^\\d+$");

            // Validate TTL.
            if (!regex.IsMatch(TTL.Text) || int.Parse(TTL.Text) < 1 || int.Parse(TTL.Text) > 255)
            {
                ShowError("请输入 1 到 255 之间的 TTL。", AdvancedTab, TTL);
                return false;
            }

            // Apply TTL.
            ApplicationOptions.TTL = int.Parse(TTL.Text);

            if (RandomPacketSizeOption.IsChecked == true)
            {
                int minSize;
                int maxSize;
                if (!regex.IsMatch(RandomPacketMin.Text) || !regex.IsMatch(RandomPacketMax.Text) ||
                    !int.TryParse(RandomPacketMin.Text, out minSize) ||
                    !int.TryParse(RandomPacketMax.Text, out maxSize) ||
                    minSize < 0 || maxSize < minSize || maxSize > ApplicationOptions.MaximumSafePayloadBytes)
                {
                    ShowError($"随机包大小必须满足：0 ≤ 最小值 ≤ 最大值 ≤ {ApplicationOptions.MaximumSafePayloadBytes}。", AdvancedTab, RandomPacketMin);
                    return false;
                }

                ApplicationOptions.PayloadMode = ApplicationOptions.PayloadSizeMode.RandomRange;
                ApplicationOptions.RandomPayloadMin = minSize;
                ApplicationOptions.RandomPayloadMax = maxSize;
                ApplicationOptions.FixedPayloadSize = minSize;
                ApplicationOptions.Buffer = new byte[minSize];
                ApplicationOptions.UseCustomBuffer = false;
            }
            // Validate packet size.
            else if (PacketSizeOption.IsChecked == true)
            {
                if (!regex.IsMatch(PacketSize.Text) || int.Parse(PacketSize.Text) < 0 || int.Parse(PacketSize.Text) > ApplicationOptions.MaximumSafePayloadBytes)
                {
                    ShowError($"固定包大小必须在 0 到 {ApplicationOptions.MaximumSafePayloadBytes} 字节之间。", AdvancedTab, PacketSize);
                    return false;
                }

                // Apply packet size.
                ApplicationOptions.FixedPayloadSize = int.Parse(PacketSize.Text);
                ApplicationOptions.Buffer = new byte[ApplicationOptions.FixedPayloadSize];
                ApplicationOptions.PayloadMode = ApplicationOptions.PayloadSizeMode.Fixed;
                ApplicationOptions.UseCustomBuffer = false;

                // Fill buffer with default text.
                if (ApplicationOptions.Buffer.Length >= 33)
                {
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes(Constants.DefaultIcmpData), 0, ApplicationOptions.Buffer, 0, 33);
                }
            }
            else
            {
                // Use custom packet data.
                ApplicationOptions.Buffer = Encoding.ASCII.GetBytes(PacketData.Text);
                ApplicationOptions.FixedPayloadSize = ApplicationOptions.Buffer.Length;
                ApplicationOptions.PayloadMode = ApplicationOptions.PayloadSizeMode.Fixed;
                ApplicationOptions.UseCustomBuffer = true;
            }

            // Apply fragment / don't fragment option.
            if (DontFragment.IsChecked == true)
            {
                ApplicationOptions.DontFragment = true;
            }
            else
            {
                ApplicationOptions.DontFragment = false;
            }

            int recordRouteHopCount;
            if (!int.TryParse(RecordRouteHopCount.Text, out recordRouteHopCount) ||
                recordRouteHopCount < 1 ||
                recordRouteHopCount > 9)
            {
                recordRouteHopCount = 9;
            }

            ApplicationOptions.ReverseResolveAddress = ReverseResolveAddress.IsChecked == true;
            ApplicationOptions.RecordRouteEnabled = RecordRouteEnabled.IsChecked == true;
            ApplicationOptions.RecordRouteHopCount = recordRouteHopCount;
            ApplicationOptions.SourceAddress = SourceAddress.Text.Trim();
            ApplicationOptions.LooseSourceRoute = LooseSourceRoute.Text.Trim();

            // Update ping options (TTL / Don't fragment settings)
            ApplicationOptions.UpdatePingOptions();

            return true;
        }

        private bool SaveAudioAlertOptions()
        {
            if (IsAudioDownAlertEnabled.IsChecked == true)
            {
                try
                {
                    if (Path.GetFileName(AudioDownFilePath.Text).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                        !File.Exists(AudioDownFilePath.Text) ||
                        Path.GetFileName(AudioDownFilePath.Text).Length < 1)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    ShowError("指定路径不存在，请输入有效路径。", AudioAlertTab, AudioDownFilePath);
                    return false;
                }
                ApplicationOptions.IsAudioDownAlertEnabled = true;
                ApplicationOptions.AudioDownFilePath = AudioDownFilePath.Text;
            }
            else
            {
                ApplicationOptions.IsAudioDownAlertEnabled = false;
            }

            if (IsAudioUpAlertEnabled.IsChecked == true)
            {
                try
                {
                    if (Path.GetFileName(AudioUpFilePath.Text).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                        !File.Exists(AudioUpFilePath.Text) ||
                        Path.GetFileName(AudioUpFilePath.Text).Length < 1)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    ShowError("指定路径不存在，请输入有效路径。", AudioAlertTab, AudioUpFilePath);
                    return false;
                }
                ApplicationOptions.IsAudioUpAlertEnabled = true;
                ApplicationOptions.AudioUpFilePath = AudioUpFilePath.Text;
            }
            else
            {
                ApplicationOptions.IsAudioUpAlertEnabled = false;
            }

            return true;
        }

        private bool SaveLogOutputOptions()
        {
            if (IsLogOutputEnabled.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(LogPath.Text))
                {
                    LogPath.Text = ApplicationOptions.GetDefaultLogDirectory();
                }

                try
                {
                    LogPath.Text = ApplicationOptions.NormalizeUserPath(LogPath.Text);
                    Directory.CreateDirectory(ApplicationOptions.ToPhysicalIoPath(LogPath.Text));
                    ApplicationOptions.IsLogOutputEnabled = true;
                    ApplicationOptions.LogPath = LogPath.Text;
                    WriteLogEnabledMarker();
                }
                catch
                {
                    string defaultLogPath = ApplicationOptions.GetDefaultLogDirectory();
                    Directory.CreateDirectory(ApplicationOptions.ToPhysicalIoPath(defaultLogPath));
                    LogPath.Text = defaultLogPath;
                    ApplicationOptions.IsLogOutputEnabled = true;
                    ApplicationOptions.LogPath = defaultLogPath;
                    WriteLogEnabledMarker();
                }
            }
            else
            {
                ApplicationOptions.IsLogOutputEnabled = false;
            }

            if (IsLogStatusChangesEnabled.IsChecked == true)
            {
                try
                {
                    LogStatusChangesPath.Text = ApplicationOptions.NormalizeUserPath(LogStatusChangesPath.Text);
                    string directory = Path.GetDirectoryName(LogStatusChangesPath.Text);
                    if (Path.GetFileName(LogStatusChangesPath.Text).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                        string.IsNullOrWhiteSpace(directory) ||
                        Path.GetFileName(LogStatusChangesPath.Text).Length < 1)
                    {
                        throw new Exception();
                    }

                    Directory.CreateDirectory(ApplicationOptions.ToPhysicalIoPath(directory));
                }
                catch
                {
                    string defaultStatusLogPath = ApplicationOptions.GetDefaultStatusLogPath();
                    Directory.CreateDirectory(ApplicationOptions.ToPhysicalIoPath(Path.GetDirectoryName(defaultStatusLogPath)));
                    LogStatusChangesPath.Text = defaultStatusLogPath;
                }

                ApplicationOptions.IsLogStatusChangesEnabled = true;
                ApplicationOptions.LogStatusChangesPath = ApplicationOptions.NormalizeUserPath(LogStatusChangesPath.Text);
            }
            else
            {
                ApplicationOptions.IsLogStatusChangesEnabled = false;
            }

            return true;
        }

        private bool SaveDisplayOptions()
        {
            ApplicationOptions.Theme = DarkThemeOption.IsChecked == true
                ? ApplicationOptions.ThemeMode.Dark
                : ApplicationOptions.ThemeMode.Light;
            ThemeManager.ApplyCurrentTheme();

            ApplicationOptions.IsAlwaysOnTopEnabled = IsAlwaysOnTopEnabled.IsChecked == true;
            ApplicationOptions.IsMinimizeToTrayEnabled = IsMinimizeToTrayEnabled.IsChecked == true;
            ApplicationOptions.IsExitToTrayEnabled = IsExitToTrayEnabled.IsChecked == true;

            return true;
        }

        private bool SaveLayoutOptions()
        {
            // Validate input.
            foreach (var control in ColorsDockPanel.GetChildren())
            {
                if (control is TextBox box)
                {
                    if (!Util.IsValidHtmlColor(box.Text))
                    {
                    ShowError("请输入有效的 HTML 颜色值。支持格式：#RGB、#RRGGBB、#AARRGGBB，例如 #3266CF。", LayoutTab, box);
                        box.SelectAll();

                        return false;
                    }
                }
            }

            ApplicationOptions.BackgroundColor_Probe_Inactive = BackgroundColor_Probe_Inactive.Text;
            ApplicationOptions.BackgroundColor_Probe_Up = BackgroundColor_Probe_Up.Text;
            ApplicationOptions.BackgroundColor_Probe_Down = BackgroundColor_Probe_Down.Text;
            ApplicationOptions.BackgroundColor_Probe_Indeterminate = BackgroundColor_Probe_Indeterminate.Text;
            ApplicationOptions.BackgroundColor_Probe_Error = BackgroundColor_Probe_Error.Text;
            ApplicationOptions.ForegroundColor_Probe_Inactive = ForegroundColor_Probe_Inactive.Text;
            ApplicationOptions.ForegroundColor_Probe_Up = ForegroundColor_Probe_Up.Text;
            ApplicationOptions.ForegroundColor_Probe_Down = ForegroundColor_Probe_Down.Text;
            ApplicationOptions.ForegroundColor_Probe_Indeterminate = ForegroundColor_Probe_Indeterminate.Text;
            ApplicationOptions.ForegroundColor_Probe_Error = ForegroundColor_Probe_Error.Text;
            ApplicationOptions.ForegroundColor_Stats_Inactive = ForegroundColor_Stats_Inactive.Text;
            ApplicationOptions.ForegroundColor_Stats_Up = ForegroundColor_Stats_Up.Text;
            ApplicationOptions.ForegroundColor_Stats_Down = ForegroundColor_Stats_Down.Text;
            ApplicationOptions.ForegroundColor_Stats_Indeterminate = ForegroundColor_Stats_Indeterminate.Text;
            ApplicationOptions.ForegroundColor_Stats_Error = ForegroundColor_Stats_Error.Text;
            ApplicationOptions.ForegroundColor_Alias_Inactive = ForegroundColor_Alias_Inactive.Text;
            ApplicationOptions.ForegroundColor_Alias_Up = ForegroundColor_Alias_Up.Text;
            ApplicationOptions.ForegroundColor_Alias_Down = ForegroundColor_Alias_Down.Text;
            ApplicationOptions.ForegroundColor_Alias_Indeterminate = ForegroundColor_Alias_Indeterminate.Text;
            ApplicationOptions.ForegroundColor_Alias_Error = ForegroundColor_Alias_Error.Text;

            return true;
        }


        private void NumericTextbox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9.-]+");
            if (regex.IsMatch(e.Text))
            {
                e.Handled = true;
            }
        }

        private void HtmlColor_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[#a-fA-F0-9]");
            if (!regex.IsMatch(e.Text))
            {
                e.Handled = true;
            }
        }

        private void BrowseLogPath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "请选择日志文件保存位置。";
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    LogPath.Text = dialog.SelectedPath;
                    ApplyLogSettingsLive();
                }
            }
        }

        private void BrowseLogStatusChangesPath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "请选择状态变化日志保存位置。";
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    LogStatusChangesPath.Text = Path.Combine(dialog.SelectedPath, $"yoping-status_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    ApplyLogSettingsLive();
                }
            }
        }

        private void LogOutputSetting_Changed(object sender, RoutedEventArgs e)
        {
            ApplyLogSettingsLive();
        }

        private void ApplyLogSettingsLive()
        {
            if (!IsLoaded)
            {
                return;
            }

            if (IsLogOutputEnabled.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(LogPath.Text))
                {
                    LogPath.Text = ApplicationOptions.GetDefaultLogDirectory();
                }
                try
                {
                    LogPath.Text = ApplicationOptions.NormalizeUserPath(LogPath.Text);
                    Directory.CreateDirectory(ApplicationOptions.ToPhysicalIoPath(LogPath.Text));
                    ApplicationOptions.LogPath = LogPath.Text;
                    ApplicationOptions.IsLogOutputEnabled = true;
                    WriteLogEnabledMarker();
                }
                catch
                {
                    ApplicationOptions.IsLogOutputEnabled = false;
                }
            }
            else
            {
                ApplicationOptions.IsLogOutputEnabled = false;
            }

            if (IsLogStatusChangesEnabled.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(LogStatusChangesPath.Text))
                {
                    LogStatusChangesPath.Text = ApplicationOptions.GetDefaultStatusLogPath();
                }
                try
                {
                    LogStatusChangesPath.Text = ApplicationOptions.NormalizeUserPath(LogStatusChangesPath.Text);
                    string directory = Path.GetDirectoryName(LogStatusChangesPath.Text);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(ApplicationOptions.ToPhysicalIoPath(directory));
                    }
                    ApplicationOptions.LogStatusChangesPath = LogStatusChangesPath.Text;
                    ApplicationOptions.IsLogStatusChangesEnabled = true;
                }
                catch
                {
                    ApplicationOptions.IsLogStatusChangesEnabled = false;
                }
            }
            else
            {
                ApplicationOptions.IsLogStatusChangesEnabled = false;
            }
        }

        private void WriteLogEnabledMarker()
        {
            try
            {
                string ioLogPath = ApplicationOptions.ToPhysicalIoPath(ApplicationOptions.LogPath);
                Directory.CreateDirectory(ioLogPath);
                File.AppendAllText(
                    Path.Combine(ioLogPath, $"yoping-log-test_{DateTime.Now:yyyyMMdd_HHmmss}.txt"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 日志已启用，保存目录：{ApplicationOptions.LogPath}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch
            {
                // 即时测试记录失败时不打断设置保存，实际 Ping 写入失败会提示。
            }
        }

        private void AudioDownBrowse_Click(object sender, RoutedEventArgs e)
        {
            AudioFileBrowse(AudioDownFilePath);
        }

        private void SourceInterface_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourceInterface.SelectedItem is SourceInterfaceOption option &&
                !string.IsNullOrWhiteSpace(option.Address))
            {
                SourceAddress.Text = option.Address;
            }
        }

        private void AudioUpBrowse_Click(object sender, RoutedEventArgs e)
        {
            AudioFileBrowse(AudioUpFilePath);
        }

        private void AudioFileBrowse(TextBox tb)
        {
            using (var audiofileDialog = new System.Windows.Forms.OpenFileDialog())
            {
                audiofileDialog.Title = "Select an audio file";
                audiofileDialog.RestoreDirectory = true;
                audiofileDialog.Multiselect = false;
                audiofileDialog.Filter = "WAV files (*.wav)|*.wav|All files|*.*";
                audiofileDialog.DefaultExt = ".wav";

                if (audiofileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    tb.Text = audiofileDialog.FileName;
                }
            }
        }

        private void AudioDownPlay_Click(object sender, RoutedEventArgs e)
        {
            AudioFilePlay(AudioDownFilePath.Text);
        }

        private void AudioUpPlay_Click(object sender, RoutedEventArgs e)
        {
            AudioFilePlay(AudioUpFilePath.Text);
        }

        private void AudioFilePlay(string path)
        {
            try
            {
                using (var player = new SoundPlayer(path))
                {
                    player.Play();
                }
            }
            catch
            {
                ShowError("无法播放音频文件。", AudioAlertTab, AudioAlertTab);
            }
        }

        private void IsAudioDownAlertEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (AudioDownFilePath.Text.Length == 0)
            {
                if (File.Exists(Environment.ExpandEnvironmentVariables(Constants.DefaultAudioDownFilePath)))
                {
                    AudioDownFilePath.Text = Environment.ExpandEnvironmentVariables(Constants.DefaultAudioDownFilePath);
                }
            }
        }

        private void IsAudioUpAlertEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (AudioUpFilePath.Text.Length == 0)
            {
                if (File.Exists(Environment.ExpandEnvironmentVariables(Constants.DefaultAudioUpFilePath)))
                {
                    AudioUpFilePath.Text = Environment.ExpandEnvironmentVariables(Constants.DefaultAudioUpFilePath);
                }
            }
        }

        private void UpdateByteCount()
        {
            var regex = new Regex("^\\d+$");
            if (PacketSizeOption.IsChecked == true)
            {
                if (PacketSize != null && regex.IsMatch(PacketSize.Text))
                {
                    Bytes.Text = (int.Parse(PacketSize.Text) + 28).ToString();
                }
                else
                {
                    Bytes.Text = "?";
                }
            }
            else if (RandomPacketSizeOption.IsChecked == true)
            {
                if (RandomPacketMin != null && RandomPacketMax != null &&
                    regex.IsMatch(RandomPacketMin.Text) && regex.IsMatch(RandomPacketMax.Text))
                {
                    Bytes.Text = $"{int.Parse(RandomPacketMin.Text) + 28} - {int.Parse(RandomPacketMax.Text) + 28}";
                }
                else
                {
                    Bytes.Text = "?";
                }
            }
            else
            {
                Bytes.Text = (PacketData.Text.Length + 28).ToString();
            }
        }

        private void PacketData_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateByteCount();
        }

        private void PacketSizeOption_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdateByteCount();
            }
        }

        private void UseCustomPacketOption_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdateByteCount();
            }
        }

        private void RestoreDefaultColors_Click(object sender, RoutedEventArgs e)
        {
            BackgroundColor_Probe_Inactive.Text = Constants.Color_Probe_Background_Inactive;
            BackgroundColor_Probe_Up.Text = Constants.Color_Probe_Background_Up;
            BackgroundColor_Probe_Down.Text = Constants.Color_Probe_Background_Down;
            BackgroundColor_Probe_Error.Text = Constants.Color_Probe_Background_Error;
            BackgroundColor_Probe_Indeterminate.Text = Constants.Color_Probe_Background_Indeterminate;
            ForegroundColor_Probe_Inactive.Text = Constants.Color_Probe_Foreground_Inactive;
            ForegroundColor_Probe_Up.Text = Constants.Color_Probe_Foreground_Up;
            ForegroundColor_Probe_Down.Text = Constants.Color_Probe_Foreground_Down;
            ForegroundColor_Probe_Error.Text = Constants.Color_Probe_Foreground_Error;
            ForegroundColor_Probe_Indeterminate.Text = Constants.Color_Probe_Foreground_Indeterminate;
            ForegroundColor_Stats_Inactive.Text = Constants.Color_Statistics_Foreground_Inactive;
            ForegroundColor_Stats_Up.Text = Constants.Color_Statistics_Foreground_Up;
            ForegroundColor_Stats_Down.Text = Constants.Color_Statistics_Foreground_Down;
            ForegroundColor_Stats_Error.Text = Constants.Color_Statistics_Foreground_Error;
            ForegroundColor_Stats_Indeterminate.Text = Constants.Color_Statistics_Foreground_Inactive;
            ForegroundColor_Alias_Inactive.Text = Constants.Color_Alias_Foreground_Inactive;
            ForegroundColor_Alias_Up.Text = Constants.Color_Alias_Foreground_Up;
            ForegroundColor_Alias_Down.Text = Constants.Color_Alias_Foreground_Down;
            ForegroundColor_Alias_Error.Text = Constants.Color_Alias_Foreground_Error;
            ForegroundColor_Alias_Indeterminate.Text = Constants.Color_Alias_Foreground_Indeterminate;
        }

        private MainWindow GetMainWindow()
        {
            return Owner as MainWindow ?? Application.Current.MainWindow as MainWindow;
        }

        private void SaveFavorite_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.CreateFavoriteFromCurrentProbes();
        }

        private void ManageFavorites_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.OpenManageFavorites();
        }

        private void ManageAliases_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.OpenManageAliases();
        }

        private void NewInstance_Click(object sender, RoutedEventArgs e)
        {
            GetMainWindow()?.LaunchNewInstance();
        }

        private void OpenHelp_Click(object sender, RoutedEventArgs e)
        {
            if (HelpWindow._OpenWindow == null || HelpWindow._OpenWindow.IsLoaded == false)
            {
                new HelpWindow { Owner = this }.Show();
            }
            else
            {
                HelpWindow._OpenWindow.Activate();
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // Hide minimize and maximize buttons.
            IntPtr _windowHandle = new WindowInteropHelper(this).Handle;
            if (_windowHandle == null)
            {
                return;
            }

            SetWindowLong(_windowHandle, GWL_STYLE, GetWindowLong(_windowHandle, GWL_STYLE) & ~WS_MAXIMIZEBOX & ~WS_MINIMIZEBOX);
        }
    }
}
