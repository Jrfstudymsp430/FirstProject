---
name: scada-app
description: Use this skill for ANY work on FirstProject / ScadaApp — the WPF Modbus RTU SCADA HMI. Apply when the user mentions 通道, 串口, COM, 点表, 标签, 实时监视, 曲线, 写入, 功能码, Float CDAB, 布局, 主题, 配置, channels.json, 从机ID, 轮询, 日志, 标题栏, or just asks to change/fix/add something in this repo. This is the project playbook; use it even if they do not say SCADA or Modbus.
---

# ScadaApp 项目手册

用简体中文回复。先读本文件；改协议/配置/窗口/曲线时再按需打开 `references/`。

## 产品

Windows 上位机：多串口 Modbus RTU 采集。仓库根目录 `/workspace`，应用在 `src/ScadaApp/`。

- 框架：**WPF + .NET 9**（`net9.0-windows`），MVVM（CommunityToolkit.Mvvm）
- 通信：NModbus RTU，一通道一口
- 功能码只允许 **03 / 06 / 16（0x10）**。采集一律 FC 03；06/16 只表示可写
- 类型：**Float CDAB**（两寄存器：先 CD 低字、后 AB 高字）+ 06 用 UInt16
- 从机 ID 在**通道级**；保存通道时同步到该通道全部 `TagPoint.SlaveId`。标签对话框不填从站
- 配置：与 exe 同目录 `channels.json`（Debug/Release/publish）。旧 `%AppData%\ScadaApp\channels.json` 可迁移
- 连接成功后状态保持「已连接」；轮询失败只记日志，不清空状态栏/日志栏连接文案
- 曲线：自绘 `TrendChart`，**禁止 LiveCharts / ScottPlot / OxyPlot**

Linux Cloud Agent **编不了 WPF**，不要假装 `dotnet build` 已通过。Windows 上：

```bash
dotnet run --project src/ScadaApp/ScadaApp.csproj
```

## 动手前

1. 读现有同类文件再改，不要另起一套风格。
2. 用户常在本机改 XAML（边距、通道名 `Foreground="Green"`、文案）。**不要擅自改回**这些本地布局。
3. 用户若明确说推 `main`：`commit` + `git pull --rebase origin main` + `push origin main`。rebase 冲突时保留用户布局，只合入功能修复。
4. 未指定推送方式时，按当前环境的分支/PR 流程做。
5. 新窗口必须 `WindowStyle=None` + 自定义标题栏，跟现有对话框一致。

## 目录（改哪里）

| 要改的事 | 文件 |
| --- | --- |
| 主界面布局 | `Views/MainWindow.xaml` |
| 主题/颜色/按钮 | `Themes/ScadaTheme.xaml` |
| 命令与点表逻辑 | `ViewModels/MainViewModel.cs` |
| 点位显示/写入/曲线缓冲 | `ViewModels/TagItemViewModel.cs` |
| 串口读写、CDAB | `Services/ModbusRtuClient.cs` |
| 轮询与连接状态 | `Services/ChannelService.cs` |
| 配置读写 | `Services/ConfigStorage.cs` |
| 通道对话框/串口保存 | `Views/ChannelConfigDialog.xaml(.cs)` |
| 标签对话框 | `Views/TagConfigDialog.xaml(.cs)` |
| 写入框 | `Views/InputDialog.xaml(.cs)` |
| 曲线绘制 | `Controls/TrendChart.cs` |
| 曲线窗口 | `Views/TagTrendWindow.xaml(.cs)` |
| 历史缓冲 | `Services/TrendBuffer.cs`, `TrendStore.cs` |

更细的文件说明见 [references/architecture.md](references/architecture.md)。已知坑见 [references/gotchas.md](references/gotchas.md)。

## 界面约定

主窗口：`WindowStyle=None`，`WindowChrome CaptionHeight=0`，标题栏按钮 `IsHitTestVisibleInChrome`。最大化用工作区而不是系统最大化（`WindowWorkAreaHelper`）。

主栏列宽约 `3*` / `8*`。右侧上「实时监视」（卡片：名称、大号当前值、迷你曲线、质量、时间、曲线/写入），下「点表配置」（名称、功能码、地址、类型、操作）。通道参数在左侧通道栏，启动/停止上方。

配色（不要提亮除非用户要求）：`#131B26` / `#182232` / `#1E2B3E`，强调色 `#2EE6C0`、`#4EC9F5`。

对话框：无系统白标题栏。写入框尤其容易漏 `WindowStyle=None`。

## 通信与数据

- 通道参数对话框字段：名称、串口、波特率、从机 ID、轮询间隔。不要加回校验/停止/数据位，除非用户要。
- 可编辑 ComboBox 选中**不会**自动写回 `PortName`。确定时必须 `UpdateSource`，并显式读 `SelectedItem` / `Text`；当前口不在系统列表也要插入。
- `TagValue.NumericValue` 为缩放后的数值；曲线只记 `Quality == Good`。
- `TrendStore` 按 tagId 复用缓冲，刷新点表不能丢历史。
- 清空日志不得把「已连接」改成离线。
- 通道卡片和状态栏显示收发报文数：每次 Modbus 请求 +1 发，收到应答 +1 收。超时/失败只计发。停止后保留本次计数，重新启动从 0 开始。

## 退出

`Window_Closing`：`e.Cancel = true` → `Hide()` → `await ShutdownAsync()` → `Dispatcher.InvokeAsync(Close)`。**禁止**在 Closing 同步路径里再 `Close()`。

## 曲线

`TrendChart`：环形缓冲 + 按像素 min/max 降采样 + `StreamGeometry`。卡片用 `Compact=True`；大图在 `TagTrendWindow`。不要引入第三方图表库。

## 改完自检

- 功能码仍只有 03/06/16，采集仍是 03，Float 仍是 CDAB
- 未改用户本机边距/绿色通道名等布局偏好
- 新窗口无系统标题栏
- 连接状态与退出流程没被破坏
- 串口保存仍写回 `PortName`
