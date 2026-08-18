using System.IO;
using System.Reflection;
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

    /// <summary>
    /// 与 exe 同目录：bin/Debug、bin/Release 或 publish 下的 channels.json。
    /// </summary>
    public static string ConfigPath => Path.Combine(AppDirectory, "channels.json");

    private static string AppDirectory
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(dir))
                dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return string.IsNullOrWhiteSpace(dir)
                ? Directory.GetCurrentDirectory()
                : dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static string LegacyConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScadaApp", "channels.json");

    public static List<ChannelConfig> Load()
    {
        try
        {
            TryMigrateLegacy();

            if (!File.Exists(ConfigPath))
            {
                var defaults = CreateDefaultChannels();
                Save(defaults);
                return defaults;
            }

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
        var list = channels.ToList();
        Sanitize(list);
        Directory.CreateDirectory(AppDirectory);
        var json = JsonSerializer.Serialize(list, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    private static void TryMigrateLegacy()
    {
        try
        {
            if (File.Exists(ConfigPath) || !File.Exists(LegacyConfigPath))
                return;

            Directory.CreateDirectory(AppDirectory);
            File.Copy(LegacyConfigPath, ConfigPath, overwrite: false);
        }
        catch
        {
            // 旧路径迁移失败时忽略，使用默认配置
        }
    }

    private static List<ChannelConfig> CreateDefaultChannels()
    {
        var channel = new ChannelConfig
        {
            Name = "通道1",
            PortName = "COM1",
            BaudRate = 9600,
            PollingIntervalMs = 1000,
            SlaveId = 1,
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
        foreach (var channel in channels)
        {
            channel.Name = TrimOrEmpty(channel.Name);
            channel.PortName = TrimOrEmpty(channel.PortName);
            if (channel.SlaveId is < 1 or > 247)
            {
                var fromTag = channel.Tags.FirstOrDefault()?.SlaveId ?? 1;
                channel.SlaveId = fromTag is >= 1 and <= 247 ? fromTag : (byte)1;
            }

            foreach (var tag in channel.Tags)
            {
                tag.Name = TrimOrEmpty(tag.Name);
                tag.Unit = TrimOrEmpty(tag.Unit);
                tag.SlaveId = channel.SlaveId;
                if (tag.DecimalPlaces is < 0 or > 12)
                    tag.DecimalPlaces = 2;
                if (!Enum.IsDefined(tag.FunctionCode))
                    tag.FunctionCode = ModbusFunctionCode.ReadHoldingRegisters;
                if (!Enum.IsDefined(tag.DataType))
                    tag.DataType = TagDataType.Float32;
            }
        }
    }

    private static string TrimOrEmpty(string? value) => value?.Trim() ?? string.Empty;
}
