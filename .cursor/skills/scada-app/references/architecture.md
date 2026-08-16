# 架构与文件

仓库：https://github.com/Jrfstudymsp430/FirstProject  
下文路径相对 `src/ScadaApp/`。

## 解决方案

- `ScadaApp.sln`
- `src/ScadaApp/ScadaApp.csproj`：`net9.0-windows`，`UseWPF`，图标 `Assets/app.ico`
- 包：CommunityToolkit.Mvvm 8.4、NModbus / NModbus.Serial 3.0.72、System.IO.Ports 9.0

## 分层

```
Views/          XAML + 少量 code-behind（窗口铬、对话框确定）
ViewModels/     命令、集合、状态
Services/       串口、轮询、配置、曲线缓冲
Models/         ChannelConfig、TagPoint、TagValue、枚举
Controls/       TrendChart 自绘
Themes/         ScadaTheme.xaml（App.xaml 合并）
Converters/     状态/质量/时间等
Helpers/        WindowWorkAreaHelper
```

## 运行时关系

`MainViewModel` 持有 `ChannelManager` + `TrendStore`。

- 启动通道 → `ChannelService.StartAsync` 打开串口 → 状态 `Connected` → 后台 `PollLoop` 对每个启用点 `ReadTagAsync`（始终 FC 03）
- `TagValueUpdated` 切回 UI 线程 → `TagItemViewModel.Update` → 写 `TrendBuffer`
- 未选中通道的点也要写入 `TrendStore`，避免切走就丢曲线
- 写入：06 → `WriteSingleRegister`（UInt16）；16 → `WriteMultipleRegisters`（Float CDAB 两寄存器）

## 模型要点

`ChannelConfig`：Id、Name、PortName、BaudRate、PollingIntervalMs、SlaveId、Tags。校验/停止/数据位字段仍在模型里（NModbus 需要），界面不再编辑。

`TagPoint`：Id、Name、SlaveId（由通道同步）、FunctionCode、Address、DataType、Unit、Scale、Offset、IsEnabled。`IsWritable` 由 06/16 决定。`RegisterCount`：Float32=2，否则 1。

`TagValue`：RawValue、NumericValue、DisplayValue、Quality、Timestamp、ErrorMessage。

`ModbusFunctionCode`：`0x03` / `0x06` / `0x10`。

## 主界面结构

`MainWindow` 外层：自定义标题栏 + 内容。

内容行：工具栏统计 → 分隔 → 主区（左通道 / 右监视+点表）→ 通信日志 → 状态栏。

监视卡片：`DataMonitorCard`，绑定 `Tags`。曲线按钮走 `ShowTagTrendCommand`。

通道列表摘要：`COM @ 波特率 | n 点`，下一行 `发 / 收` 绑定 `TxCount`/`RxCount`。通道名绿色是用户本地样式，不要改。

## 窗口清单

| 窗口 | 用途 |
| --- | --- |
| MainWindow | 主界面 |
| ChannelConfigDialog | 通道参数 |
| TagConfigDialog | 点表项（无从站地址） |
| InputDialog | 写入值 |
| BatchWriteDialog | 连续点批量写入 |
| MessageDialog | 确认/提示 |
| TagTrendWindow | 点位曲线（非模态，每点一窗） |

## 配置

`ConfigStorage.ConfigPath` = `AppContext.BaseDirectory/channels.json`。JSON camelCase、缩进。`Sanitize` 会整理空名和从机范围。
