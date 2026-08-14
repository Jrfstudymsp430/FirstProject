# SCADA - Modbus RTU 多通道采集系统

基于 **WPF (.NET 8)** 的 SCADA 上位机软件，支持多串口通道并行通信，遵循 **Modbus RTU** 标准协议。

## 功能特性

- **多通道通信**：每个通道独立对应一个串口（COM），可并行采集
- **Modbus RTU 标准协议**：基于 NModbus 库，支持常用功能码
  - `0x01` 读线圈 (Read Coils)
  - `0x02` 读离散输入 (Read Discrete Inputs)
  - `0x03` 读保持寄存器 (Read Holding Registers)
  - `0x04` 读输入寄存器 (Read Input Registers)
  - `0x05` 写单个线圈 (Write Single Coil)
  - `0x06` 写单个寄存器 (Write Single Register)
  - `0x10` 写多个寄存器 (Write Multiple Registers)
- **实时数据采集**：可配置轮询间隔，实时显示数据点质量与时间戳
- **数据类型支持**：Bool、Int16、UInt16、Int32、UInt32、Float32
- **读写控制**：支持对可写标签进行 Modbus 写入
- **配置持久化**：通道与标签配置自动保存至 `%AppData%\ScadaApp\channels.json`
- **通信日志**：记录连接、读写及异常信息

## 系统要求

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022（推荐，需安装「.NET 桌面开发」工作负载）

> WPF 仅支持 Windows 平台，无法在 Linux/macOS 上运行。

## 快速开始

```bash
# 克隆仓库后
cd ScadaApp

# 还原依赖并编译
dotnet restore
dotnet build

# 运行
dotnet run --project src/ScadaApp/ScadaApp.csproj
```

或在 Visual Studio 中打开 `ScadaApp.sln`，按 F5 运行。

## 使用说明

1. **添加通道**：点击左侧 `+` 添加串口通道，配置 COM 口、波特率、轮询间隔
2. **配置标签**：选中通道后点击「添加标签」，设置从站地址、功能码、寄存器地址、数据类型
3. **启动采集**：点击「启动通道」或「启动全部」开始 Modbus RTU 轮询
4. **写入数据**：对标记为「可写入」的标签，点击「写入」按钮下发 Modbus 写命令
5. **保存配置**：点击「保存配置」将当前通道/标签配置写入本地文件

## 项目结构

```
ScadaApp.sln
src/ScadaApp/
├── Models/          # 通道、标签、Modbus 功能码等数据模型
├── Services/        # Modbus RTU 客户端、多通道管理、配置存储
├── ViewModels/      # MVVM 视图模型
├── Views/           # WPF 界面
├── Converters/      # 值转换器
└── Themes/          # 深色 SCADA 主题
```

## 架构说明

```
┌─────────────────────────────────────────────────┐
│                  MainWindow (UI)                 │
└─────────────────────┬───────────────────────────┘
                      │ MVVM
┌─────────────────────▼───────────────────────────┐
│              ChannelManager                      │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────┐ │
│  │ ChannelService│ │ ChannelService│ │   ...    │ │
│  │   (COM1)     │ │   (COM2)     │ │          │ │
│  └──────┬───────┘ └──────┬───────┘ └──────────┘ │
└─────────┼────────────────┼───────────────────────┘
          │                │
┌─────────▼────────────────▼───────────────────────┐
│           ModbusRtuClient (NModbus RTU)          │
│              SerialPort (RS-485/RS-232)          │
└──────────────────────────────────────────────────┘
```

## 依赖库

| 包名 | 用途 |
|------|------|
| NModbus / NModbus.Serial | Modbus RTU 协议实现 |
| CommunityToolkit.Mvvm | MVVM 框架 |
| System.IO.Ports | 串口通信 |

## 注意事项

- 同一 COM 口不可被多个程序同时占用
- Modbus RTU 从站地址范围为 1–247
- Float32 / Int32 类型占用 2 个连续寄存器（高字在前）
- 使用 USB 转 RS-485 适配器时，请确认驱动已安装并在设备管理器中识别 COM 口

## 许可证

MIT
