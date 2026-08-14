using System.IO;
using System.Text.Json;
using ScadaApp.Models;

namespace ScadaApp.Services;

public static class ConfigStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string ConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScadaApp", "channels.json");

    public static List<ChannelConfig> Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return CreateDefaultChannels();

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<List<ChannelConfig>>(json, JsonOptions) ?? CreateDefaultChannels();
        }
        catch
        {
            return CreateDefaultChannels();
        }
    }

    public static void Save(IEnumerable<ChannelConfig> channels)
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(channels.ToList(), JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    private static List<ChannelConfig> CreateDefaultChannels()
    {
        var channel = new ChannelConfig
        {
            Name = "通道1 - COM1",
            PortName = "COM1",
            BaudRate = 9600,
            PollingIntervalMs = 1000,
            Tags =
            {
                new TagPoint
                {
                    Name = "温度",
                    SlaveId = 1,
                    FunctionCode = ModbusFunctionCode.ReadHoldingRegisters,
                    Address = 0,
                    DataType = TagDataType.Float32,
                    Unit = "°C",
                    Scale = 0.1
                },
                new TagPoint
                {
                    Name = "运行状态",
                    SlaveId = 1,
                    FunctionCode = ModbusFunctionCode.ReadCoils,
                    Address = 0,
                    DataType = TagDataType.Bool
                },
                new TagPoint
                {
                    Name = "设定值",
                    SlaveId = 1,
                    FunctionCode = ModbusFunctionCode.ReadHoldingRegisters,
                    Address = 10,
                    DataType = TagDataType.UInt16,
                    IsWritable = true
                }
            }
        };

        return new List<ChannelConfig> { channel };
    }
}
