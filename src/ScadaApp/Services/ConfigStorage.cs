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
            var loaded = JsonSerializer.Deserialize<List<ChannelConfig>>(json, JsonOptions) ?? CreateDefaultChannels();
            Sanitize(loaded);
            return loaded;
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
                    Unit = "°C"
                },
                new TagPoint
                {
                    Name = "设定值",
                    SlaveId = 1,
                    FunctionCode = ModbusFunctionCode.WriteMultipleRegisters,
                    Address = 2,
                    DataType = TagDataType.Float32,
                    Unit = "°C"
                }
            }
        };

        return new List<ChannelConfig> { channel };
    }

    private static void Sanitize(List<ChannelConfig> channels)
    {
        foreach (var tag in channels.SelectMany(c => c.Tags))
        {
            if (!Enum.IsDefined(tag.FunctionCode))
                tag.FunctionCode = ModbusFunctionCode.ReadHoldingRegisters;
            if (!Enum.IsDefined(tag.DataType))
                tag.DataType = TagDataType.Float32;
        }
    }
}
