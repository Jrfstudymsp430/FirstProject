using CommunityToolkit.Mvvm.ComponentModel;
using ScadaApp.Models;
using ScadaApp.Services;

namespace ScadaApp.ViewModels;

public partial class ChannelItemViewModel : ObservableObject
{
    private readonly IChannelManager _channelManager;

    [ObservableProperty] private ChannelState _state = ChannelState.Disconnected;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private long _txCount;
    [ObservableProperty] private long _rxCount;

    public ChannelItemViewModel(IChannelManager channelManager, ChannelConfig config)
    {
        _channelManager = channelManager;
        Config = config;
        Config.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ChannelConfig.Name) or nameof(ChannelConfig.PortName) or nameof(ChannelConfig.BaudRate))
            {
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(PortName));
                OnPropertyChanged(nameof(BaudRate));
                OnPropertyChanged(nameof(Summary));
            }
        };
    }

    public ChannelConfig Config { get; }

    public string Id => Config.Id;
    public string Name => Config.Name;
    public string PortName => Config.PortName;
    public int BaudRate => Config.BaudRate;
    public int TagCount => Config.Tags.Count;
    public string Summary => $"{Config.PortName} @ {Config.BaudRate} | {Config.Tags.Count} 点";
    public string PacketStats => $"发 {TxCount}  收 {RxCount}";

    public void RefreshState()
    {
        var running = _channelManager.GetRunningChannel(Config.Id);
        var traffic = _channelManager.GetTraffic(Config.Id);
        TxCount = traffic.TxCount;
        RxCount = traffic.RxCount;
        if (running != null)
        {
            State = running.State;
            IsRunning = running.State == ChannelState.Connected || running.State == ChannelState.Connecting;
        }
        else
        {
            State = ChannelState.Disconnected;
            IsRunning = false;
        }
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(PacketStats));
    }
}
