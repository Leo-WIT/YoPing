using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using vmPing.UI;

namespace vmPing.Classes
{
    public partial class Probe
    {
        public void StartStop()
        {
            if (string.IsNullOrWhiteSpace(Hostname))
            {
                return;
            }

            if (IsActive)
            {
                // Stopping probe.
                StopProbe(ProbeStatus.Inactive);
                return;
            }

            // Starting probe.
            CancelSource = new CancellationTokenSource();

            if (Hostname.StartsWith("#"))
            {
                Type = ProbeType.Comment;
                return;
            }

            if (Hostname.StartsWith("D/"))
            {
                Type = ProbeType.Dns;
                Hostname = Hostname.Substring(2);
                PerformDnsLookup(CancelSource.Token);
                return;
            }

            if (Hostname.StartsWith("T/"))
            {
                Type = ProbeType.Traceroute;
                Hostname = Hostname.Substring(2);
                PerformTraceroute(CancelSource.Token);
                return;
            }

            Type = ProbeType.Ping;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (mutex)
                {
                    StatusChangeLog.Add(new StatusChangeLog
                    {
                        Timestamp = DateTime.Now,
                        Hostname = Hostname,
                        Alias = Alias,
                        Status = ProbeStatus.Start
                    });
                }
            }));

            if (IsTcpPing(Hostname))
            {
                Task.Run(() => PerformTcpProbe(CancelSource.Token), CancelSource.Token);
            }
            else
            {
                Task.Run(() => PerformIcmpProbe(CancelSource.Token), CancelSource.Token);
            }
        }

        private static bool IsTcpPing(string hostname)
        {
            return hostname.Count(f => f == ':') == 1 || hostname.Contains("]:");
        }

        private void InitializeProbe()
        {
            IsActive = true;
            StartTime = DateTime.Now;
            Status = ProbeStatus.Inactive;
            Statistics.Reset();
            IndeterminateCount = 0;
            HighLatencyCount = 0;
            MinRtt = long.MaxValue;
            History = new ObservableCollection<string>();
            logFileTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            WriteToLog($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  目标={Hostname}  状态=开始  间隔={ApplicationOptions.PingInterval}ms  TTL={ApplicationOptions.TTL}  模式={ApplicationOptions.StopMode}");
        }

        private void StopProbe(ProbeStatus status)
        {
            CancelSource.Cancel();
            Status = status;
            IsActive = false;

            if (status != ProbeStatus.Error)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    lock (mutex)
                    {
                        WriteFinalStatisticsToHistory();
                    }
                }));
            }

            AddStatusHistory(ProbeStatus.Stop);
        }

        private void AddStatusHistory(ProbeStatus status, bool isHidden = false)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (mutex)
                {
                    StatusChangeLog.Add(new StatusChangeLog
                    {
                        Timestamp = DateTime.Now,
                        Hostname = Hostname,
                        Alias = Alias,
                        Status = status,
                        HasStatusBeenCleared = isHidden
                    });
                }
            }));
        }

        private async Task<bool> IsHostInvalid(string host, CancellationToken token)
        {
            try
            {
                switch (Uri.CheckHostName(host))
                {
                    case UriHostNameType.IPv4:
                    case UriHostNameType.IPv6:
                        // IP address was entered. No further action necessary.
                        break;
                    case UriHostNameType.Dns:
                        var ipAddresses = await Dns.GetHostAddressesAsync(host);
                        token.ThrowIfCancellationRequested();
                        if (ipAddresses.Length > 0)
                        {
                            await Application.Current.Dispatcher.BeginInvoke(
                                new Action(() => AddHistory($"    ({ipAddresses[0]})")));
                        }
                        break;
                    default:
                        throw new Exception();
                }
                return false;
            }
            catch
            {
                if (!token.IsCancellationRequested)
                {
                    await Application.Current.Dispatcher.BeginInvoke(
                        new Action(() => AddHistory($"{Environment.NewLine}Unable to resolve hostname")));
                }
                return true;
            }
        }

        private void WriteToLog(string message)
        {
            if (!ApplicationOptions.IsLogOutputEnabled)
            {
                return;
            }

            try
            {
                string directory = string.IsNullOrWhiteSpace(ApplicationOptions.LogPath)
                    ? ApplicationOptions.GetDefaultLogDirectory()
                    : ApplicationOptions.NormalizeUserPath(ApplicationOptions.LogPath);
                ApplicationOptions.LogPath = directory;
                string ioDirectory = ApplicationOptions.ToPhysicalIoPath(directory);
                Directory.CreateDirectory(ioDirectory);
                string timestamp = string.IsNullOrWhiteSpace(logFileTimestamp)
                    ? DateTime.Now.ToString("yyyyMMdd_HHmmss")
                    : logFileTimestamp;
                string logPath = Path.Combine(ioDirectory, $"{Util.GetSafeFilename(Hostname)}_{timestamp}.txt");

                lock (logWriteLock)
                {
                    File.AppendAllText(logPath, message + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                ApplicationOptions.IsLogOutputEnabled = false;
                RecordLogErrorOnce($"日志写入失败，已自动关闭普通日志。原因：{ex.Message}");
            }
        }

        private void WriteToStatusChangesLog(StatusChangeLog status)
        {
            if (!ApplicationOptions.IsLogStatusChangesEnabled || string.IsNullOrEmpty(ApplicationOptions.LogStatusChangesPath))
            {
                return;
            }

            try
            {
                ApplicationOptions.LogStatusChangesPath = ApplicationOptions.NormalizeUserPath(ApplicationOptions.LogStatusChangesPath);
                string directory = Path.GetDirectoryName(ApplicationOptions.LogStatusChangesPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(ApplicationOptions.ToPhysicalIoPath(directory));
                }
                string ioStatusLogPath = ApplicationOptions.ToPhysicalIoPath(ApplicationOptions.LogStatusChangesPath);

                lock (logWriteLock)
                {
                    File.AppendAllText(ioStatusLogPath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{status.Hostname}\t{status.Alias}\t{status.StatusAsString}{Environment.NewLine}",
                        Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                ApplicationOptions.IsLogStatusChangesEnabled = false;
                RecordLogErrorOnce($"状态变化日志写入失败，已自动关闭状态日志。原因：{ex.Message}");
            }
        }

        private void RecordLogErrorOnce(string message)
        {
            if (hasShownLogWriteError)
            {
                return;
            }

            hasShownLogWriteError = true;
            lastLogWriteError = message;
        }

        private void DisplayStatistics()
        {
            StatisticsText =
                $"发送 {Statistics.Sent}  成功 {Statistics.Received}  丢包 {Statistics.LossRate:0.#}%  " +
                $"最小 {Statistics.MinRtt}ms  最大 {Statistics.MaxRtt}ms  平均 {Statistics.AverageRtt:0.#}ms";
        }

        private bool ShouldStopByExecutionRule()
        {
            if (ApplicationOptions.StopMode == ApplicationOptions.TerminationMode.Continuous)
            {
                return false;
            }

            if (ApplicationOptions.StopMode == ApplicationOptions.TerminationMode.Count)
            {
                return Statistics.Sent >= ApplicationOptions.PingCountLimit;
            }

            return (DateTime.Now - StartTime).TotalSeconds >= ApplicationOptions.RunDurationSeconds;
        }

        private void OnStatusChange(ProbeStatus newStatus, string alertType)
        {
            Status = newStatus;
            TriggerStatusChange(new StatusChangeLog
            {
                Timestamp = DateTime.Now,
                Hostname = Hostname,
                Alias = Alias,
                Status = newStatus
            });

            // 邮件提醒功能已移除。
            ApplicationOptions.IsEmailAlertEnabled = false;
        }

        private void TriggerStatusChange(StatusChangeLog status)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                bool shouldPopup = ApplicationOptions.PopupOption == ApplicationOptions.PopupNotificationOption.Always
                    || (ApplicationOptions.PopupOption == ApplicationOptions.PopupNotificationOption.WhenMinimized
                    && Application.Current.MainWindow.WindowState == WindowState.Minimized);

                lock (mutex)
                {
                    if (shouldPopup && !Application.Current.Windows.OfType<PopupNotificationWindow>().Any())
                    {
                        foreach (var entry in StatusChangeLog)
                        {
                            entry.HasStatusBeenCleared = true;
                        }
                    }

                    StatusChangeLog.Add(status);
                }

                if (shouldPopup && !Application.Current.Windows.OfType<PopupNotificationWindow>().Any())
                {
                    new PopupNotificationWindow(StatusChangeLog).Show();
                }
            }));

            if (ApplicationOptions.IsLogStatusChangesEnabled)
            {
                lock (mutex)
                {
                    WriteToStatusChangesLog(status);
                }
            }

            if ((ApplicationOptions.IsAudioDownAlertEnabled) && (status.Status == ProbeStatus.Down))
            {
                try
                {
                    using (SoundPlayer player = new SoundPlayer(ApplicationOptions.AudioDownFilePath))
                    {
                        player.Play();
                    }
                }
                catch (Exception ex)
                {
                    ApplicationOptions.IsAudioDownAlertEnabled = false;
                    ShowError($"Failed to play audio file. Audio alerts have been disabled. Error: {ex.Message}");
                }
            }
            else if ((ApplicationOptions.IsAudioUpAlertEnabled) && (status.Status == ProbeStatus.Up))
            {
                try
                {
                    using (SoundPlayer player = new SoundPlayer(ApplicationOptions.AudioUpFilePath))
                    {
                        player.Play();
                    }
                }
                catch (Exception ex)
                {
                    ApplicationOptions.IsAudioUpAlertEnabled = false;
                    ShowError($"Failed to play audio file. Audio alerts have been disabled. Error: {ex.Message}");
                }
            }
        }

        private void ShowError(string message)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                DialogWindow.ErrorWindow(message).ShowDialog()));
        }
    }
}
