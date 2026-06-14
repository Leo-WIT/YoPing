using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace vmPing.Classes
{
    public partial class Probe
    {
        private async void PerformIcmpProbe(CancellationToken cancellationToken)
        {
            InitializeProbe();

            await Application.Current.Dispatcher.BeginInvoke(
                new Action(() => AddHistory($"*** Pinging {Hostname}:")));

            if (ApplicationOptions.UseWindowsNativePing)
            {
                await Application.Current.Dispatcher.BeginInvoke(
                    new Action(() => AddHistory("已启用 Windows Ping 专用参数，使用系统 ping.exe 执行。")));
                await PerformWindowsNativePingProbe(cancellationToken);
                return;
            }

            if (await IsHostInvalid(Hostname, cancellationToken))
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    StopProbe(ProbeStatus.Error);
                }
                return;
            }

            using (var ping = new Ping())
            {
                bool hasConcurrencySlot = false;
                try
                {
                    await ApplicationOptions.PingConcurrencyGate.WaitAsync(cancellationToken);
                    hasConcurrencySlot = true;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            if (ShouldStopByExecutionRule())
                            {
                                StopProbe(ProbeStatus.Inactive);
                                break;
                            }

                        // Send ping.
                        Statistics.Sent++;
                        var payload = ApplicationOptions.CreatePayloadBuffer();
                        var reply = await ping.SendPingAsync(
                            hostNameOrAddress: Hostname,
                            timeout: ApplicationOptions.PingTimeout,
                            buffer: payload,
                            options: ApplicationOptions.PingOptions);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        // Reply received.
                        if (reply.Status == IPStatus.Success)
                        {
                            Statistics.Received++;
                            Statistics.RecordSuccess(reply.RoundtripTime);
                            IndeterminateCount = 0;

                            // If this is a new probe, record the initial 'up' state to the status history.
                            if (Status == ProbeStatus.Inactive)
                            {
                                AddStatusHistory(ProbeStatus.Up, true);
                                Status = ProbeStatus.Up;
                            }

                            // Check if status changed from down to up.
                            if (Status == ProbeStatus.Down)
                            {
                                OnStatusChange(ProbeStatus.Up, "up");
                            }

                            // Update minimum RTT.
                            if (reply.RoundtripTime < MinRtt)
                            {
                                MinRtt = reply.RoundtripTime;
                            }

                            // Check latency.
                            if ((ApplicationOptions.LatencyDetectionMode == ApplicationOptions.LatencyMode.Fixed &&
                                reply.RoundtripTime >= ApplicationOptions.HighLatencyMilliseconds) ||
                                (ApplicationOptions.LatencyDetectionMode == ApplicationOptions.LatencyMode.Auto &&
                                reply.RoundtripTime >= MinRtt + ApplicationOptions.HighLatencyMilliseconds))
                            {
                                // Latency is high.
                                if (HighLatencyCount < ApplicationOptions.HighLatencyAlertTiggerCount)
                                {
                                    HighLatencyCount++;
                                }

                                if (Status == ProbeStatus.Up)
                                {
                                    Status = ProbeStatus.Indeterminate;
                                }

                                if (Status != ProbeStatus.LatencyHigh)
                                {
                                    if (HighLatencyCount >= ApplicationOptions.HighLatencyAlertTiggerCount)
                                    {
                                        OnStatusChange(ProbeStatus.LatencyHigh, "high latency");
                                    }
                                }
                            }
                            else
                            {
                                // Latency is normal.
                                if (HighLatencyCount > 0)
                                {
                                    HighLatencyCount--; 
                                }

                                if (Status == ProbeStatus.LatencyHigh)
                                {
                                    if (HighLatencyCount <= 0)
                                    {
                                        OnStatusChange(ProbeStatus.LatencyNormal, "normal latency");
                                        Status = ProbeStatus.Up;
                                    }
                                }
                                else
                                {
                                    Status = ProbeStatus.Up;
                                }
                            }
                        }
                        // No reply received.
                        else
                        {
                            Statistics.Lost++;
                            IndeterminateCount++;

                            if (Status == ProbeStatus.Up || Status == ProbeStatus.LatencyHigh)
                            {
                                Status = ProbeStatus.Indeterminate;
                            }
                            else if (Status == ProbeStatus.Inactive)
                            {
                                // Because this is a new probe, ignore the indeterminate count
                                // and immediately mark the host as down.
                                // Also, record the initial 'down' state to the status history.
                                AddStatusHistory(ProbeStatus.Down, true);
                                Status = ProbeStatus.Down;
                            }

                            // Check for status change.
                            if (Status == ProbeStatus.Indeterminate &&
                                IndeterminateCount >= ApplicationOptions.AlertThreshold)
                            {
                                Status = ProbeStatus.Down;
                                OnStatusChange(ProbeStatus.Down, "down");
                            }
                        }

                        if (!cancellationToken.IsCancellationRequested)
                        {
                            // Update output.
                            DisplayStatistics();
                            DisplayIcmpReply(reply, null, payload.Length);

                            // Pause between probes.
                            await IcmpWait(reply.Status);
                        }
                    }

                    catch (Exception ex)
                    {
                        Statistics.Lost++;

                        // Check for status change.
                        if (Status == ProbeStatus.Inactive)
                        {
                            Status = ProbeStatus.Down;
                        }

                        if (Status != ProbeStatus.Down)
                        {
                            Status = ProbeStatus.Down;
                            OnStatusChange(ProbeStatus.Down, "error");
                        }

                        // Update output.
                        DisplayStatistics();
                        DisplayIcmpReply(null, ex, 0);

                        // Pause between probes.
                        await Task.Delay(ApplicationOptions.PingInterval);
                    }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常停止或取消等待并发槽位，不视为错误。
                }
                finally
                {
                    if (hasConcurrencySlot)
                    {
                        ApplicationOptions.PingConcurrencyGate.Release();
                    }
                }
            }
        }

        private async Task IcmpWait(IPStatus ipStatus)
        {
            if (ipStatus == IPStatus.TimedOut)
            {
                // Ping timed out. If the ping interval is greater than the timeout,
                // then sleep for [INTERVAL - TIMEOUT]
                // Otherwise, sleep for a fixed amount of 1 second
                if (ApplicationOptions.PingInterval > ApplicationOptions.PingTimeout)
                {
                    await Task.Delay(ApplicationOptions.PingInterval - ApplicationOptions.PingTimeout);
                }
                else
                {
                    await Task.Delay(1000);
                }
            }
            else
            {
                // For any other type of ping response, sleep for the global ping interval amount
                // before sending another ping.
                await Task.Delay(ApplicationOptions.PingInterval);
            }
        }

        private void DisplayIcmpReply(PingReply pingReply, Exception ex = null, int payloadSize = 0)
        {
            if (pingReply == null && ex == null)
            {
                return;
            }

            // Build output string based on the ping reply details.
            var sb = new StringBuilder($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  目标={Hostname}  序号={Statistics.Sent}  ");

            if (pingReply != null)
            {
                switch (pingReply.Status)
                {
                    case IPStatus.Success:
                        sb.Append("状态=成功  来源=");
                        sb.Append(pingReply.Address.ToString());
                        sb.Append(pingReply.RoundtripTime < 1
                            ? "  延迟=<1ms"
                            : $"  延迟={pingReply.RoundtripTime}ms");
                        sb.Append($"  TTL={ApplicationOptions.TTL}  包大小={payloadSize}B");
                        break;
                    case IPStatus.DestinationHostUnreachable:
                        sb.Append("Reply  [Host unreachable]");
                        break;
                    case IPStatus.DestinationNetworkUnreachable:
                        sb.Append("Reply  [Network unreachable]");
                        break;
                    case IPStatus.DestinationUnreachable:
                        sb.Append("Reply  [Unreachable]");
                        break;
                    case IPStatus.TimedOut:
                        sb.Append($"状态=超时  延迟=-  TTL={ApplicationOptions.TTL}  包大小={payloadSize}B");
                        break;
                    default:
                        sb.Append($"状态={pingReply.Status}  TTL={ApplicationOptions.TTL}  包大小={payloadSize}B");
                        break;
                }
            }
            else
            {
                sb.Append(ex.InnerException is SocketException
                    ? "状态=解析失败"
                    : ex.Message);
            }

            var output = sb.ToString();

            // Add response to the output window..
            Application.Current.Dispatcher.BeginInvoke(
                new Action(() => AddHistory(output)));

            // If enabled, log output.
            WriteToLog(output);
        }
    }
}
