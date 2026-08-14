using CommunityToolkit.Mvvm.ComponentModel;

namespace ScadaApp.Models;

/// <summary>
/// 串口通道配置（一个通道对应一个 COM 口）
/// </summary>
public partial class ChannelConfig : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString();
    [ObservableProperty] private string _name = "通道1";
    [ObservableProperty] private string _portName = "COM1";
    [ObservableProperty] private int _baudRate = 9600;
    [ObservableProperty] private int _dataBits = 8;
    [ObservableProperty] private System.IO.Ports.Parity _parity = System.IO.Ports.Parity.None;
    [ObservableProperty] private System.IO.Ports.StopBits _stopBits = System.IO.Ports.StopBits.One;
    [ObservableProperty] private int _readTimeout = 1000;
    [ObservableProperty] private int _writeTimeout = 1000;
    [ObservableProperty] private int _pollingIntervalMs = 500;
    [ObservableProperty] private bool _isEnabled = true;

    public List<TagPoint> Tags { get; set; } = new();
}
