using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using vmPing.Properties;
using vmPing.UI;

namespace vmPing.Classes
{
    internal static class Configuration
    {
        public static string FilePath = Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\YoPing\YoPing.xml");

        static Configuration()
        {
            // If a configuration file exists in the current directory, use it (portable mode).
            // Otherwise, the default location is used (%LocalAppData%\YoPing).
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "YoPing.xml"))
            {
                FilePath = AppDomain.CurrentDomain.BaseDirectory + "YoPing.xml";
            }
        }

        public static bool Exists()
        {
            return File.Exists(FilePath);
        }

        public static bool IsReady()
        {
            if (Exists())
            {
                return true;
            }

            // No configuration found.
            // Prompt the user to create a new configuration file.
            var newConfigWindow = new NewConfigurationWindow();

            // Find the best owner for the new window.
            // Checks: Owned windows of MainWindow, plus owned windows of any child windows.
            var mainOwnedWindows = Application.Current.MainWindow.OwnedWindows;
            if (mainOwnedWindows.Count > 0)
            {
                newConfigWindow.Owner = mainOwnedWindows[0].OwnedWindows.Count > 0
                    ? mainOwnedWindows[0].OwnedWindows[0]
                    : mainOwnedWindows[0];
            }
            else
            {
                newConfigWindow.Owner = Application.Current.MainWindow;
            }

            // Display the new configuration window.
            // FilePath is updated if the user chooses portable mode.
            if (newConfigWindow.ShowDialog() == false)
            {
                // User cancelled.
                return false;
            }

            // Create the directory if it doesn't exist.
            if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                }
                catch (Exception ex)
                {
                    Util.ShowError($"{Strings.Error_CreateDirectory} {ex.Message}");
                    return false;
                }
            }

            // Create a minimal XML configuration file.
            try
            {
                var xd = new XDocument(
                    new XElement("vmping",
                        new XElement("aliases"),
                        new XElement("favorites"),
                        new XElement("configuration"),
                        new XElement("colors")));
                xd.Save(FilePath);

                return true;
            }
            catch (Exception ex)
            {
                Util.ShowError($"{Strings.Error_CreateConfig} {ex.Message}");
                return false;
            }
        }

        public static string GetEscapedXpath(string xpath)
        {
            if (!xpath.Contains("'"))
            {
                return '\'' + xpath + '\'';
            }
            else if (!xpath.Contains("\""))
            {
                return '"' + xpath + '"';
            }
            else
            {
                return "concat('" + xpath.Replace("'", "',\"'\",'") + "')";
            }
        }

        public static void Save()
        {
            if (!IsReady())
            {
                return;
            }

            ApplicationOptions.LogPath = ApplicationOptions.NormalizeUserPath(ApplicationOptions.LogPath);
            ApplicationOptions.LogStatusChangesPath = ApplicationOptions.NormalizeUserPath(ApplicationOptions.LogStatusChangesPath);

            try
            {
                // Open XML configuration file and get root <vmping> node.
                var xd = XDocument.Load(FilePath);
                XElement root = xd.Element("vmping")
                    ?? throw new XmlException("Invalid configuration file.");

                // Delete old nodes then recreate them with current config.
                root.Descendants("configuration").Remove();
                root.Descendants("colors").Remove();
                root.Add(GenerateConfigurationNode());
                root.Add(GenerateColorsNode());

                xd.Save(FilePath);
            }
            catch (Exception ex)
            {
                Util.ShowError($"{Strings.Error_WriteConfig} {ex.Message}");
            }
        }

        private static XElement GenerateConfigurationNode()
        {
            // In XML, options are written as:
            // <configuration>
            //   <option name="MyOptionName">myValue</option>
            // </configuration>

            // Local function to create nodes.
            XElement Node(string name, object value) =>
                new XElement("option", new XAttribute("name", name), value ?? string.Empty);

            return new XElement("configuration",
                Node("PingInterval", ApplicationOptions.PingInterval),
                Node("PingTimeout", ApplicationOptions.PingTimeout),
                Node("StopMode", ApplicationOptions.StopMode),
                Node("PingCountLimit", ApplicationOptions.PingCountLimit),
                Node("RunDurationSeconds", ApplicationOptions.RunDurationSeconds),
                Node("MaxConcurrentTasks", ApplicationOptions.MaxConcurrentTasks),
                Node("TTL", ApplicationOptions.TTL),
                Node("DontFragment", ApplicationOptions.DontFragment),
                Node("ReverseResolveAddress", ApplicationOptions.ReverseResolveAddress),
                Node("RecordRouteEnabled", ApplicationOptions.RecordRouteEnabled),
                Node("RecordRouteHopCount", ApplicationOptions.RecordRouteHopCount),
                Node("SourceAddress", ApplicationOptions.SourceAddress),
                Node("LooseSourceRoute", ApplicationOptions.LooseSourceRoute),
                Node("UseCustomBuffer", ApplicationOptions.UseCustomBuffer),
                Node("Buffer", Encoding.ASCII.GetString(ApplicationOptions.Buffer)),
                Node("PayloadMode", ApplicationOptions.PayloadMode),
                Node("FixedPayloadSize", ApplicationOptions.FixedPayloadSize),
                Node("RandomPayloadMin", ApplicationOptions.RandomPayloadMin),
                Node("RandomPayloadMax", ApplicationOptions.RandomPayloadMax),
                Node("AlertThreshold", ApplicationOptions.AlertThreshold),
                new XComment(" InitialStartMode: [Blank, MultiInput, Favorite] "),
                Node("InitialStartMode", ApplicationOptions.InitialStartMode),
                Node("InitialProbeCount", ApplicationOptions.InitialProbeCount),
                Node("InitialColumnCount", ApplicationOptions.InitialColumnCount),
                Node("InitialFavorite", ApplicationOptions.InitialFavorite),
                new XComment(" PopupNotifications: [Always, Never, WhenMinimized] "),
                Node("PopupNotifications", ApplicationOptions.PopupOption),
                Node("IsAutoDismissEnabled", ApplicationOptions.IsAutoDismissEnabled),
                Node("AutoDismissMilliseconds", ApplicationOptions.AutoDismissMilliseconds),
                Node("IsAudioUpAlertEnabled", ApplicationOptions.IsAudioUpAlertEnabled),
                Node("AudioUpFilePath", ApplicationOptions.AudioUpFilePath),
                Node("IsAudioDownAlertEnabled", ApplicationOptions.IsAudioDownAlertEnabled),
                Node("AudioDownFilePath", ApplicationOptions.AudioDownFilePath),
                Node("IsLogOutputEnabled", ApplicationOptions.IsLogOutputEnabled),
                Node("LogPath", ApplicationOptions.LogPath),
                Node("IsLogStatusChangesEnabled", ApplicationOptions.IsLogStatusChangesEnabled),
                Node("LogStatusChangesPath", ApplicationOptions.LogStatusChangesPath),
                new XComment(" Theme: [Light, Dark] "),
                Node("Theme", ApplicationOptions.Theme),
                Node("IsAlwaysOnTopEnabled", ApplicationOptions.IsAlwaysOnTopEnabled),
                Node("IsMinimizeToTrayEnabled", ApplicationOptions.IsMinimizeToTrayEnabled),
                Node("IsExitToTrayEnabled", ApplicationOptions.IsExitToTrayEnabled)
            );
        }

        private static XElement GenerateColorsNode()
        {
            // In XML, options are written as:
            // <colors>
            //   <option name="MyOptionName">myValue</option>
            // </colors>

            // Local function to create nodes.
            XElement Node(string name, object value) =>
                new XElement("option", new XAttribute("name", name), value ?? string.Empty);

            return new XElement("colors",
                new XComment(" Probe background "),
                Node("Probe.Background.Inactive", ApplicationOptions.BackgroundColor_Probe_Inactive),
                Node("Probe.Background.Up", ApplicationOptions.BackgroundColor_Probe_Up),
                Node("Probe.Background.Down", ApplicationOptions.BackgroundColor_Probe_Down),
                Node("Probe.Background.Indeterminate", ApplicationOptions.BackgroundColor_Probe_Indeterminate),
                Node("Probe.Background.Error", ApplicationOptions.BackgroundColor_Probe_Error),

                new XComment(" Probe foreground "),
                Node("Probe.Foreground.Inactive", ApplicationOptions.ForegroundColor_Probe_Inactive),
                Node("Probe.Foreground.Up", ApplicationOptions.ForegroundColor_Probe_Up),
                Node("Probe.Foreground.Down", ApplicationOptions.ForegroundColor_Probe_Down),
                Node("Probe.Foreground.Indeterminate", ApplicationOptions.ForegroundColor_Probe_Indeterminate),
                Node("Probe.Foreground.Error", ApplicationOptions.ForegroundColor_Probe_Error),

                new XComment(" Statistics foreground "),
                Node("Statistics.Foreground.Inactive", ApplicationOptions.ForegroundColor_Stats_Inactive),
                Node("Statistics.Foreground.Up", ApplicationOptions.ForegroundColor_Stats_Up),
                Node("Statistics.Foreground.Down", ApplicationOptions.ForegroundColor_Stats_Down),
                Node("Statistics.Foreground.Indeterminate", ApplicationOptions.ForegroundColor_Stats_Indeterminate),
                Node("Statistics.Foreground.Error", ApplicationOptions.ForegroundColor_Stats_Error),

                new XComment(" Alias foreground "),
                Node("Alias.Foreground.Inactive", ApplicationOptions.ForegroundColor_Alias_Inactive),
                Node("Alias.Foreground.Up", ApplicationOptions.ForegroundColor_Alias_Up),
                Node("Alias.Foreground.Down", ApplicationOptions.ForegroundColor_Alias_Down),
                Node("Alias.Foreground.Indeterminate", ApplicationOptions.ForegroundColor_Alias_Indeterminate),
                Node("Alias.Foreground.Error", ApplicationOptions.ForegroundColor_Alias_Error)
            );
        }

        public static void Load()
        {
            if (!Exists())
            {
                return;
            }

            try
            {
                var xd = new XmlDocument();
                xd.Load(FilePath);

                LoadAppOptions(xd.SelectNodes("/vmping/configuration/option"));
                LoadColors(xd.SelectNodes("/vmping/colors/option"));
                ApplicationOptions.UpdatePingOptions();
            }

            catch (Exception ex)
            {
                Util.ShowError($"{Strings.Error_LoadConfig} {ex.Message}");
            }
        }

        private static void LoadColors(XmlNodeList nodes)
        {
            if (nodes == null)
            {
                return;
            }

            var options = nodes.Cast<XmlNode>()
                .Where(n => n.Attributes?["name"] != null)
                .ToDictionary(n => n.Attributes["name"].Value, n => n.InnerText);

            // Load probe backgorund colors.
            ApplyColor("Probe.Background.Inactive", v => ApplicationOptions.BackgroundColor_Probe_Inactive = v, options);
            ApplyColor("Probe.Background.Up", v => ApplicationOptions.BackgroundColor_Probe_Up = v, options);
            ApplyColor("Probe.Background.Down", v => ApplicationOptions.BackgroundColor_Probe_Down = v, options);
            ApplyColor("Probe.Background.Indeterminate", v => ApplicationOptions.BackgroundColor_Probe_Indeterminate = v, options);
            ApplyColor("Probe.Background.Error", v => ApplicationOptions.BackgroundColor_Probe_Error = v, options);
            ApplyColor("Probe.Foreground.Inactive", v => ApplicationOptions.ForegroundColor_Probe_Inactive = v, options);
            ApplyColor("Probe.Foreground.Up", v => ApplicationOptions.ForegroundColor_Probe_Up = v, options);
            ApplyColor("Probe.Foreground.Down", v => ApplicationOptions.ForegroundColor_Probe_Down = v, options);
            ApplyColor("Probe.Foreground.Indeterminate", v => ApplicationOptions.ForegroundColor_Probe_Indeterminate = v, options);
            ApplyColor("Probe.Foreground.Error", v => ApplicationOptions.ForegroundColor_Probe_Error = v, options);
            ApplyColor("Statistics.Foreground.Inactive", v => ApplicationOptions.ForegroundColor_Stats_Inactive = v, options);
            ApplyColor("Statistics.Foreground.Up", v => ApplicationOptions.ForegroundColor_Stats_Up = v, options);
            ApplyColor("Statistics.Foreground.Down", v => ApplicationOptions.ForegroundColor_Stats_Down = v, options);
            ApplyColor("Statistics.Foreground.Indeterminate", v => ApplicationOptions.ForegroundColor_Stats_Indeterminate = v, options);
            ApplyColor("Statistics.Foreground.Error", v => ApplicationOptions.ForegroundColor_Stats_Error = v, options);
            ApplyColor("Alias.Foreground.Inactive", v => ApplicationOptions.ForegroundColor_Alias_Inactive = v, options);
            ApplyColor("Alias.Foreground.Up", v => ApplicationOptions.ForegroundColor_Alias_Up = v, options);
            ApplyColor("Alias.Foreground.Down", v => ApplicationOptions.ForegroundColor_Alias_Down = v, options);
            ApplyColor("Alias.Foreground.Indeterminate", v => ApplicationOptions.ForegroundColor_Alias_Indeterminate = v, options);
            ApplyColor("Alias.Foreground.Error", v => ApplicationOptions.ForegroundColor_Alias_Error = v, options);

            MigrateLegacyStatusColors();
        }

        private static void MigrateLegacyStatusColors()
        {
            if (ApplicationOptions.BackgroundColor_Probe_Up == "#5cb85c")
            {
                ApplicationOptions.BackgroundColor_Probe_Up = Constants.Color_Probe_Background_Up;
                ApplicationOptions.ForegroundColor_Probe_Up = Constants.Color_Probe_Foreground_Up;
                ApplicationOptions.ForegroundColor_Stats_Up = Constants.Color_Statistics_Foreground_Up;
                ApplicationOptions.ForegroundColor_Alias_Up = Constants.Color_Alias_Foreground_Up;
            }
            if (ApplicationOptions.BackgroundColor_Probe_Down == "#d9534f")
            {
                ApplicationOptions.BackgroundColor_Probe_Down = Constants.Color_Probe_Background_Down;
                ApplicationOptions.ForegroundColor_Probe_Down = Constants.Color_Probe_Foreground_Down;
                ApplicationOptions.ForegroundColor_Stats_Down = Constants.Color_Statistics_Foreground_Down;
                ApplicationOptions.ForegroundColor_Alias_Down = Constants.Color_Alias_Foreground_Down;
            }
            if (ApplicationOptions.BackgroundColor_Probe_Indeterminate == "#f0ad4e")
            {
                ApplicationOptions.BackgroundColor_Probe_Indeterminate = Constants.Color_Probe_Background_Indeterminate;
                ApplicationOptions.ForegroundColor_Probe_Indeterminate = Constants.Color_Probe_Foreground_Indeterminate;
                ApplicationOptions.ForegroundColor_Stats_Indeterminate = Constants.Color_Statistics_Foreground_Indeterminate;
                ApplicationOptions.ForegroundColor_Alias_Indeterminate = Constants.Color_Alias_Foreground_Indeterminate;
            }
            if (ApplicationOptions.BackgroundColor_Probe_Error == "#f0ad4e")
            {
                ApplicationOptions.BackgroundColor_Probe_Error = Constants.Color_Probe_Background_Error;
                ApplicationOptions.ForegroundColor_Probe_Error = Constants.Color_Probe_Foreground_Error;
                ApplicationOptions.ForegroundColor_Stats_Error = Constants.Color_Statistics_Foreground_Error;
                ApplicationOptions.ForegroundColor_Alias_Error = Constants.Color_Alias_Foreground_Error;
            }
            if (ApplicationOptions.BackgroundColor_Probe_Inactive == "#f5f5f5")
            {
                ApplicationOptions.BackgroundColor_Probe_Inactive = Constants.Color_Probe_Background_Inactive;
                ApplicationOptions.ForegroundColor_Probe_Inactive = Constants.Color_Probe_Foreground_Inactive;
                ApplicationOptions.ForegroundColor_Stats_Inactive = Constants.Color_Statistics_Foreground_Inactive;
                ApplicationOptions.ForegroundColor_Alias_Inactive = Constants.Color_Alias_Foreground_Inactive;
            }
        }

        private static void ApplyColor(string key, Action<string> setter, IDictionary<string, string> options)
        {
            if (options.TryGetValue(key, out var value) && Util.IsValidHtmlColor(value))
                setter(value);
        }

        private static void LoadAppOptions(XmlNodeList nodes)
        {
            if (nodes == null)
            {
                return;
            }

            var options = nodes.Cast<XmlNode>()
                .Where(n => n.Attributes?["name"] != null)
                .ToDictionary(n => n.Attributes["name"].Value, n => n.InnerText);

            if (options.TryGetValue("PingInterval", out string optionValue))
            {
                ApplicationOptions.PingInterval = int.Parse(optionValue);
            }
            if (options.TryGetValue("PingTimeout", out optionValue))
            {
                ApplicationOptions.PingTimeout = int.Parse(optionValue);
            }
            if (options.TryGetValue("StopMode", out optionValue))
            {
                ApplicationOptions.StopMode = optionValue.Equals(ApplicationOptions.TerminationMode.Duration.ToString())
                    ? ApplicationOptions.TerminationMode.Duration
                    : optionValue.Equals(ApplicationOptions.TerminationMode.Count.ToString())
                        ? ApplicationOptions.TerminationMode.Count
                        : ApplicationOptions.TerminationMode.Continuous;
            }
            if (options.TryGetValue("PingCountLimit", out optionValue))
            {
                ApplicationOptions.PingCountLimit = int.Parse(optionValue);
            }
            if (options.TryGetValue("RunDurationSeconds", out optionValue))
            {
                ApplicationOptions.RunDurationSeconds = int.Parse(optionValue);
            }
            if (options.TryGetValue("MaxConcurrentTasks", out optionValue))
            {
                ApplicationOptions.MaxConcurrentTasks = int.Parse(optionValue);
            }
            if (options.TryGetValue("TTL", out optionValue))
            {
                ApplicationOptions.TTL = int.Parse(optionValue);
            }
            if (options.TryGetValue("DontFragment", out optionValue))
            {
                ApplicationOptions.DontFragment = bool.Parse(optionValue);
            }
            if (options.TryGetValue("ReverseResolveAddress", out optionValue))
            {
                ApplicationOptions.ReverseResolveAddress = bool.Parse(optionValue);
            }
            if (options.TryGetValue("RecordRouteEnabled", out optionValue))
            {
                ApplicationOptions.RecordRouteEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("RecordRouteHopCount", out optionValue))
            {
                ApplicationOptions.RecordRouteHopCount = int.Parse(optionValue);
            }
            if (options.TryGetValue("SourceAddress", out optionValue))
            {
                ApplicationOptions.SourceAddress = optionValue;
            }
            if (options.TryGetValue("LooseSourceRoute", out optionValue))
            {
                ApplicationOptions.LooseSourceRoute = optionValue;
            }
            if (options.TryGetValue("UseCustomBuffer", out optionValue))
            {
                ApplicationOptions.UseCustomBuffer = bool.Parse(optionValue);
            }
            if (options.TryGetValue("Buffer", out optionValue))
            {
                ApplicationOptions.Buffer = Encoding.ASCII.GetBytes(optionValue);
            }
            if (options.TryGetValue("PayloadMode", out optionValue))
            {
                ApplicationOptions.PayloadMode = optionValue.Equals(ApplicationOptions.PayloadSizeMode.RandomRange.ToString())
                    ? ApplicationOptions.PayloadSizeMode.RandomRange
                    : ApplicationOptions.PayloadSizeMode.Fixed;
            }
            if (options.TryGetValue("FixedPayloadSize", out optionValue))
            {
                ApplicationOptions.FixedPayloadSize = int.Parse(optionValue);
            }
            if (options.TryGetValue("RandomPayloadMin", out optionValue))
            {
                ApplicationOptions.RandomPayloadMin = int.Parse(optionValue);
            }
            if (options.TryGetValue("RandomPayloadMax", out optionValue))
            {
                ApplicationOptions.RandomPayloadMax = int.Parse(optionValue);
            }
            if (options.TryGetValue("AlertThreshold", out optionValue))
            {
                ApplicationOptions.AlertThreshold = int.Parse(optionValue);
            }
            if (options.TryGetValue("InitialStartMode", out optionValue))
            {
                if (optionValue.Equals(ApplicationOptions.StartMode.Favorite.ToString()))
                {
                    ApplicationOptions.InitialStartMode = ApplicationOptions.StartMode.Favorite;
                }
                else if (optionValue.Equals(ApplicationOptions.StartMode.MultiInput.ToString()))
                {
                    ApplicationOptions.InitialStartMode = ApplicationOptions.StartMode.MultiInput;
                }
                else
                {
                    ApplicationOptions.InitialStartMode = ApplicationOptions.StartMode.Blank;
                }
            }
            if (options.TryGetValue("InitialProbeCount", out optionValue))
            {
                ApplicationOptions.InitialProbeCount = int.Parse(optionValue);
            }
            if (options.TryGetValue("InitialColumnCount", out optionValue))
            {
                ApplicationOptions.InitialColumnCount = int.Parse(optionValue);
            }
            if (options.TryGetValue("InitialFavorite", out optionValue))
            {
                ApplicationOptions.InitialFavorite = optionValue;
            }
            if (options.TryGetValue("PopupNotifications", out optionValue))
            {
                if (optionValue.Equals(ApplicationOptions.PopupNotificationOption.Always.ToString()))
                {
                    ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.Always;
                }
                else if (optionValue.Equals(ApplicationOptions.PopupNotificationOption.WhenMinimized.ToString()))
                {
                    ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.WhenMinimized;
                }
                else
                {
                    ApplicationOptions.PopupOption = ApplicationOptions.PopupNotificationOption.Never;
                }
            }
            if (options.TryGetValue("IsAutoDismissEnabled", out optionValue))
            {
                ApplicationOptions.IsAutoDismissEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("AutoDismissMilliseconds", out optionValue))
            {
                ApplicationOptions.AutoDismissMilliseconds = int.Parse(optionValue);
            }
            ApplicationOptions.IsEmailAlertEnabled = false;
            if (options.TryGetValue("IsAudioAlertEnabled", out optionValue))
            {
                // For compatibility with version 1.3.4 and lower.
                ApplicationOptions.IsAudioDownAlertEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("AudioFilePath", out optionValue))
            {
                // For compatibility with version 1.3.4 and lower.
                ApplicationOptions.AudioDownFilePath = optionValue;
            }
            if (options.TryGetValue("IsAudioUpAlertEnabled", out optionValue))
            {
                ApplicationOptions.IsAudioUpAlertEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("IsAudioDownAlertEnabled", out optionValue))
            {
                ApplicationOptions.IsAudioDownAlertEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("AudioUpFilePath", out optionValue))
            {
                ApplicationOptions.AudioUpFilePath = optionValue;
            }
            if (options.TryGetValue("AudioDownFilePath", out optionValue))
            {
                ApplicationOptions.AudioDownFilePath = optionValue;
            }
            if (options.TryGetValue("IsLogOutputEnabled", out optionValue))
            {
                ApplicationOptions.IsLogOutputEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("LogPath", out optionValue))
            {
                ApplicationOptions.LogPath = ApplicationOptions.NormalizeUserPath(optionValue);
            }
            if (options.TryGetValue("IsLogStatusChangesEnabled", out optionValue))
            {
                ApplicationOptions.IsLogStatusChangesEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("LogStatusChangesPath", out optionValue))
            {
                ApplicationOptions.LogStatusChangesPath = ApplicationOptions.NormalizeUserPath(optionValue);
            }
            if (options.TryGetValue("Theme", out optionValue))
            {
                ApplicationOptions.Theme = optionValue.Equals(ApplicationOptions.ThemeMode.Dark.ToString())
                    ? ApplicationOptions.ThemeMode.Dark
                    : ApplicationOptions.ThemeMode.Light;
            }
            if (options.TryGetValue("IsAlwaysOnTopEnabled", out optionValue))
            {
                ApplicationOptions.IsAlwaysOnTopEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("IsMinimizeToTrayEnabled", out optionValue))
            {
                ApplicationOptions.IsMinimizeToTrayEnabled = bool.Parse(optionValue);
            }
            if (options.TryGetValue("IsExitToTrayEnabled", out optionValue))
            {
                ApplicationOptions.IsExitToTrayEnabled = bool.Parse(optionValue);
            }
        }
    }
}
