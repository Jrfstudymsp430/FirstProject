# SCADA - Modbus RTU 多通道采集系统

基于 **WPF (.NET 9)** 的 SCADA 上位机软件，支持多串口通道并行通信，遵循 **Modbus RTU** 协议。

## 功能特性

- **多通道通信**：每个通道独立对应一个串口（COM），可并行采集
- **Modbus RTU 功能码**
  - `03` 读保持寄存器（所有点采集均使用 03）
  - `06` 写单个寄存器
  - `16` 写多个寄存器
- **数据类型**：`Float CDAB`（32 位浮点，字序 CDAB）、`UInt16`
- **实时数据采集**：可配置轮询间隔，显示数据质量与时间戳
- **读写控制**：功能码 06/16 的点可写入
- **连接状态**：串口打开成功后界面持续显示「已连接」，直到手动停止
- **当前时间**：标题栏与状态栏显示系统时间
- **配置持久化**：`%AppData%\ScadaApp\channels.json`
- **通信日志**：连接、读写、异常记录，支持清空

## 系统要求

- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Visual Studio 2022（推荐，需安装「.NET 桌面开发」工作负载）

> WPF 仅支持 Windows 平台。

## 快速开始

```bash
dotnet restore
dotnet build
dotnet run --project src/ScadaApp/ScadaApp.csproj
```

或在 Visual Studio 中打开 `ScadaApp.sln`，按 F5 运行。

## 使用说明

1. **添加通道**：点击左侧 `+` 添加串口通道，配置 COM 口、波特率、轮询间隔
2. **配置标签**：选中通道后点击「添加标签」，功能码选 03/06/16，Float 使用 CDAB 字序
3. **启动采集**：点击「启动通道」或「启动全部」；连接成功后通道徽章、日志栏和状态栏保持「已连接」
4. **写入数据**：功能码 06/16 的点可点击「写入」（Float 按 CDAB 写两个寄存器）
5. **清空日志**：通信日志右上角「清空日志」（不会把连接状态改回离线）

## Float CDAB

32 位浮点占用 2 个保持寄存器：第一个寄存器为 **CD**（低字），第二个为 **AB**（高字）。

## 项目结构

```
ScadaApp.sln
src/ScadaApp/
├── Models/
├── Services/
├── ViewModels/
├── Views/
├── Converters/
└── Themes/
```

## 依赖库

| 包名 | 用途 |
|------|------|
| NModbus / NModbus.Serial | Modbus RTU |
| CommunityToolkit.Mvvm | MVVM |
| System.IO.Ports | 串口 |

## 许可证

MIT
