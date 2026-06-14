using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace vmPing.Classes
{
    public partial class Probe
    {
        private async Task PerformWindowsNativePingProbe(CancellationToken cancellationToken)
        {
            using (var pingProcess = new Process())
            {
                bool hasConcurrencySlot = false;
                try
                {
                    await ApplicationOptions.PingConcurrencyGate.WaitAsync(cancellationToken);
                    hasConcurrencySlot = true;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (ShouldStopByExecutionRule())
                        {
                            StopProbe(ProbeStatus.Inactive);
                            break;
                        }

                        Statistics.Sent++;
                        var payload = ApplicationOptions.CreatePayloadBuffer();
                        var result = await RunWindowsPingOnce(payload.Length, cancellationToken);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        if (result.Success)
                        {
                            Statistics.Received++;
                            Statistics.RecordSuccess(result.RoundTripTime);
                            IndeterminateCount = 0;

                            if (Status == ProbeStatus.Inactive)
                            {
                                AddStatusHistory(ProbeStatus.Up, true);
                                Status = ProbeStatus.Up;
                            }
                            if (Status == ProbeStatus.Down)
                            {
                                OnStatusChange(ProbeStatus.Up, "up");
                            }
                            Status = ProbeStatus.Up;
                        }
                        else
                        {
                            Statistics.Lost++;
                            IndeterminateCount++;
                            if (Status == ProbeStatus.Inactive)
                            {
                                AddStatusHistory(ProbeStatus.Down, true);
                                Status = ProbeStatus.Down;
                            }
                            else if (Status == ProbeStatus.Up && IndeterminateCount >= ApplicationOptions.AlertThreshold)
                            {
                                OnStatusChange(ProbeStatus.Down, "down");
                            }
                        }

                        DisplayStatistics();
                        DisplayWindowsPingReply(result, payload.Length);
                        await Task.Delay(ApplicationOptions.PingInterval, cancellationToken);
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

        private async Task<WindowsPingResult> RunWindowsPingOnce(int payloadSize, CancellationToken cancellationToken)
        {
            string arguments = BuildWindowsPingArguments(payloadSize);

            var startInfo = new ProcessStartInfo
            {
                FileName = "ping.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Default,
                StandardErrorEncoding = Encoding.Default
            };

            using (var process = Process.Start(startInfo))
            {
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit(), cancellationToken);

                string combinedOutput = string.IsNullOrWhiteSpace(error)
                    ? output
                    : output + Environment.NewLine + error;

                long roundTripTime = ExtractRoundTripTime(combinedOutput);
                bool success = process.ExitCode == 0 || combinedOutput.IndexOf("TTL=", StringComparison.OrdinalIgnoreCase) >= 0;

                return new WindowsPingResult
                {
                    Success = success,
                    RoundTripTime = roundTripTime,
                    CommandLine = "ping " + arguments,
                    RawOutput = combinedOutput
                };
            }
        }

        private string BuildWindowsPingArguments(int payloadSize)
        {
            var sb = new StringBuilder();
            sb.Append("-n 1 ");
            sb.Append($"-w {ApplicationOptions.PingTimeout} ");
            sb.Append($"-i {ApplicationOptions.TTL} ");
            sb.Append($"-l {payloadSize} ");

            if (ApplicationOptions.DontFragment)
            {
                sb.Append("-f ");
            }
            if (ApplicationOptions.ReverseResolveAddress)
            {
                sb.Append("-a ");
            }
            if (ApplicationOptions.RecordRouteEnabled)
            {
                int hops = Math.Max(1, Math.Min(9, ApplicationOptions.RecordRouteHopCount));
                sb.Append($"-r {hops} ");
            }
            if (!string.IsNullOrWhiteSpace(ApplicationOptions.SourceAddress))
            {
                sb.Append("-S ");
                sb.Append(QuoteArgument(ApplicationOptions.SourceAddress.Trim()));
                sb.Append(" ");
            }
            if (!string.IsNullOrWhiteSpace(ApplicationOptions.LooseSourceRoute))
            {
                sb.Append("-j ");
                sb.Append(QuoteArgument(ApplicationOptions.LooseSourceRoute.Replace(" ", "").Trim()));
                sb.Append(" ");
            }

            sb.Append(QuoteArgument(Hostname));
            return sb.ToString();
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static long ExtractRoundTripTime(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return 0;
            }

            var match = Regex.Match(output, @"(?:time|时间)[=<]\s*(\d+)\s*ms", RegexOptions.IgnoreCase);
            if (match.Success && long.TryParse(match.Groups[1].Value, out long time))
            {
                return time;
            }

            return output.Contains("<1ms") || output.Contains("<1 毫秒") ? 0 : 0;
        }

        private void DisplayWindowsPingReply(WindowsPingResult result, int payloadSize)
        {
            var statusText = result.Success ? "成功" : "失败/超时";
            var sb = new StringBuilder(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  目标={Hostname}  序号={Statistics.Sent}  状态={statusText}  延迟={(result.Success ? result.RoundTripTime + "ms" : "-")}  TTL={ApplicationOptions.TTL}  包大小={payloadSize}B  模式=Windows ping");

            if (ApplicationOptions.ReverseResolveAddress ||
                ApplicationOptions.RecordRouteEnabled ||
                !string.IsNullOrWhiteSpace(ApplicationOptions.SourceAddress) ||
                !string.IsNullOrWhiteSpace(ApplicationOptions.LooseSourceRoute))
            {
                var importantLines = result.RawOutput
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .Take(12);
                sb.Append(Environment.NewLine);
                sb.Append("命令：");
                sb.Append(result.CommandLine);
                sb.Append(Environment.NewLine);
                sb.Append(string.Join(Environment.NewLine, importantLines));
            }

            var output = sb.ToString();
            Application.Current.Dispatcher.BeginInvoke(new Action(() => AddHistory(output)));
            WriteToLog(output);
        }

        private class WindowsPingResult
        {
            public bool Success { get; set; }
            public long RoundTripTime { get; set; }
            public string CommandLine { get; set; }
            public string RawOutput { get; set; }
        }
    }
}
