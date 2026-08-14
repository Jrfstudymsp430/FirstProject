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

    public TagItemViewModel(IChannelManager channelManager, ChannelConfig channel, TagPoint tag)
    {
        _channelManager = channelManager;
        _channel = channel;
        _tag = tag;
    }

    public string TagId => _tag.Id;
    public string Name => _tag.Name;
    public byte SlaveId => _tag.SlaveId;
    public string FunctionCode => _tag.FunctionCode.ToString();
    public ushort Address => _tag.Address;
    public string DataType => _tag.DataType.ToString();
    public string Unit => _tag.Unit;
    public bool IsWritable => _tag.IsWritable;
    public string ChannelName => _channel.Name;

    public void Update(TagValue value)
    {
        if (value.TagId != TagId) return;
        DisplayValue = value.DisplayValue;
        Quality = value.Quality;
        Timestamp = value.Timestamp;
        ErrorMessage = value.ErrorMessage;
    }

    public async Task WriteAsync(string input)
    {
        object parsed = _tag.DataType switch
        {
            TagDataType.Bool => input is "1" or "ON" or "on" or "true" or "True",
            TagDataType.Int16 => short.Parse(input),
            TagDataType.UInt16 => ushort.Parse(input),
            TagDataType.Int32 => int.Parse(input),
            TagDataType.UInt32 => uint.Parse(input),
            TagDataType.Float32 => float.Parse(input),
            _ => input
        };

        await _channelManager.WriteTagAsync(_channel.Id, TagId, parsed);
    }
}
