using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ScadaApp.Models;
using ScadaApp.Services;

namespace ScadaApp.ViewModels;

public partial class TagItemViewModel : ObservableObject
{
    private readonly IChannelManager _channelManager;
    private readonly ChannelConfig _channel;
    private readonly TagPoint _tag;

    [ObservableProperty] private string _displayValue = "--";
    [ObservableProperty] private string _quality = "Bad";
    [ObservableProperty] private DateTime _timestamp;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private double? _numericValue;

    public TagItemViewModel(IChannelManager channelManager, ChannelConfig channel, TagPoint tag, TrendBuffer trend)
    {
        _channelManager = channelManager;
        _channel = channel;
        _tag = tag;
        Trend = trend;
    }

    public TrendBuffer Trend { get; }

    public string TagId => _tag.Id;
    public string Name => _tag.Name;
    public byte SlaveId => _tag.SlaveId;
    public string FunctionCode => _tag.FunctionCode switch
    {
        ModbusFunctionCode.ReadHoldingRegisters => "03 读保持寄存器",
        ModbusFunctionCode.WriteSingleRegister => "06 写单个寄存器",
        ModbusFunctionCode.WriteMultipleRegisters => "16 写多个寄存器",
        _ => _tag.FunctionCode.ToString()
    };
    public ushort Address => _tag.Address;
    public string DataType => _tag.DataType == TagDataType.Float32 ? "Float CDAB" : "UInt16";
    public string Unit => _tag.Unit;
    public bool IsWritable => _tag.IsWritable;
    public string ChannelName => _channel.Name;

    public void Update(TagValue value, bool recordTrend = true)
    {
        if (value.TagId != TagId) return;
        DisplayValue = value.DisplayValue;
        Quality = value.Quality;
        Timestamp = value.Timestamp;
        ErrorMessage = value.ErrorMessage;
        NumericValue = value.NumericValue;

        if (recordTrend && value.Quality == "Good" && value.NumericValue is double number)
            Trend.Add(value.Timestamp == default ? DateTime.Now : value.Timestamp, number);
    }

    public async Task WriteAsync(string input)
    {
        input = StripUnit(input.Trim());
        object parsed = _tag.DataType switch
        {
            TagDataType.UInt16 => ushort.Parse(input, CultureInfo.InvariantCulture),
            TagDataType.Float32 => float.Parse(input, CultureInfo.InvariantCulture),
            _ => input
        };

        await _channelManager.WriteTagAsync(_channel.Id, TagId, parsed);
    }

    private static string StripUnit(string input)
    {
        if (double.TryParse(input, out _))
            return input;

        var space = input.LastIndexOf(' ');
        if (space > 0 && double.TryParse(input[..space], out _))
            return input[..space];

        return input;
    }
}
