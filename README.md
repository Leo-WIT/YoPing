# YoPing

YoPing 是一个面向 Windows 的图形化多目标 Ping 监控工具。它支持多主机并行监控、TCP 端口探测、路由跟踪、状态历史、中文界面、现代主题、增强日志、Windows Ping 专用参数和指定出口网卡。

YoPing is a Windows desktop utility for visually monitoring multiple hosts with Ping. It supports parallel host monitoring, TCP port checks, traceroute, status history, a Chinese UI, modern themes, improved logging, Windows-specific Ping options, source-interface selection, and a refreshed application icon.

![YoPing icon](assets/yoping-golden-network-icon-final.png)

## 功能概览

- 多目标并行 Ping，每个目标独立显示状态、延迟、丢包率和历史输出。
- TCP 端口探测，输入 `host:port` 可持续检测端口连通性。
- 批量地址输入，支持换行、逗号、导入和导出。
- 路由跟踪窗口，用于快速查看到目标的路径。
- 压力测试工具，用于对指定主机持续发包测试。
- 运行摘要栏，显示 Ping 规则、间隔、超时、日志状态、并发数和 Windows Ping 模式。
- 普通日志和状态变化日志，日志文件名带时间戳。
- 浅色和暗黑主题，状态色采用低饱和监控配色。
- 收藏夹、别名、状态历史和弹窗提醒。
- 新程序图标：奶白色金毛与网络节点元素。

## Windows Ping 专用能力

YoPing 默认使用 .NET ICMP Ping，启用以下任一 Windows 专用选项后会自动切换为系统 `ping.exe`：

| 功能 | Windows 命令 | YoPing 支持方式 |
|---|---|---|
| 反向解析 IP 对应主机名 | `ping -a IP` | 设置中勾选“反向解析 IP 对应主机名” |
| 记录路由节点 | `ping -r 跳数 目标` | 设置中启用“记录路由节点”，跳数限制为 1-9 |
| 指定源地址 | `ping -S 本机IP 目标` | 设置中填写“源地址” |
| 指定出口网卡 | `ping -S 本机IP 目标` | 设置中选择“出口网卡”，自动填入该网卡 IPv4 |
| 松散源路由 | `ping -j IP1,IP2 目标` | 设置中填写“松散路由”，多个节点用逗号分隔 |

`-j` 源路由依赖网络设备支持，许多现代路由器或防火墙会禁用该能力；YoPing 会传递参数，但链路是否允许由网络环境决定。

## 使用方式

打开 `YoPing.exe` 后，在主窗口中输入主机名或 IP 地址并点击开始。常见输入格式：

```text
8.8.8.8
www.example.com
192.168.1.1
example.com:443
T/www.example.com
D/www.example.com
```

含义：

- `host` 或 `IP`：普通 ICMP Ping。
- `host:port`：TCP 端口探测。
- `T/host`：在探针窗口中执行路由跟踪。
- `D/host`：执行 DNS 查询。

批量地址可以通过菜单中的“批量地址”打开，也可以使用快捷键 `F2`。

## 设置说明

常用设置位于“设置”窗口：

- 常规：发包间隔、超时、停止规则、并发数、提醒阈值。
- 高级：TTL、包大小、不分片、Windows Ping 专用选项、出口网卡。
- 通知：状态变化弹窗。
- 声音：主机上线/离线提示音。
- 日志：普通 Ping 日志和状态变化日志。
- 显示：浅色/暗黑主题、置顶、最小化到托盘。
- 颜色：状态卡片颜色和文字颜色。

日志默认使用当前 Windows 用户的文档目录：

```text
C:\Users\<User>\Documents\YoPing\Logs
```

如果从受控或虚拟化环境启动，文件系统可能会重定向路径。需要验证真实文档目录写入时，请从 Windows 资源管理器直接启动程序。

## 构建

项目使用 WPF 和 .NET Framework 4.7.2。

推荐环境：

- Windows 10 或 Windows 11
- Visual Studio 2022
- .NET Framework 4.7.2 Developer Pack

使用 Visual Studio 打开：

```text
vmPing.sln
```

或使用 MSBuild：

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' '.\vmPing.sln' /p:Configuration=Debug /p:Platform='Any CPU'
```

构建产物位于：

```text
vmPing\bin\Debug\YoPing.exe
```

Release 版可以单文件运行，直接双击 `YoPing.exe` 即可启动。中文界面资源已内置到主程序，不需要额外复制 `zh-CN` 目录。

## Project overview

YoPing is a visual multi-host Ping monitor for Windows. It can watch many hosts at once, color-code their status, log output, run TCP port checks, trace routes, and expose several Windows-specific Ping options through a desktop UI.

## Highlights

- Monitor multiple hosts in parallel.
- Show latency, packet loss, status history, and raw output per target.
- Run TCP port checks with `host:port`.
- Import or export bulk host lists.
- Use traceroute, DNS lookup, and a stress-test window.
- Keep timestamped Ping logs and status-change logs.
- Select a source interface for Windows Ping through `ping -S`.
- Use `ping -a`, `ping -r`, `ping -S`, and `ping -j` without leaving the app.
- Switch between light and dark themes.
- Use favorites, aliases, popup alerts, tray behavior, and custom colors.

## Windows-specific Ping features

YoPing normally uses .NET ICMP Ping. When a Windows-only option is enabled, it calls the native `ping.exe` so the same behavior as the Windows command line is used.

| Feature | Windows command | In YoPing |
|---|---|---|
| Reverse-resolve an IP address | `ping -a IP` | Enable reverse resolve |
| Record route nodes | `ping -r hops target` | Enable record route, 1-9 hops |
| Specify source address | `ping -S localIP target` | Enter a source address |
| Specify source interface | `ping -S localIP target` | Select a network interface; YoPing fills its IPv4 address |
| Loose source route | `ping -j IP1,IP2 target` | Enter comma-separated route nodes |

Source-interface selection maps to Windows Ping’s source-address option. Windows does not provide a `ping` parameter for interface name directly; the selected interface’s IPv4 address is used as the source IP.

## Build from source

Open `vmPing.sln` in Visual Studio 2022 and build the solution, or run MSBuild from the repository root:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' '.\vmPing.sln' /p:Configuration=Debug /p:Platform='Any CPU'
```

The executable is generated at:

```text
vmPing\bin\Debug\YoPing.exe
```

The release build can run as a single executable. Double-click `YoPing.exe` to start it; Chinese UI resources are embedded in the main executable, so the `zh-CN` satellite folder is not required for release use.

## License

See `LICENSE` for license details. The MIT license notice is retained in the repository as required by the license text.
