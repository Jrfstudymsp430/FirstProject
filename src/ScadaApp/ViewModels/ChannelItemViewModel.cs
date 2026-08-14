using CommunityToolkit.Mvvm.ComponentModel;
using ScadaApp.Models;
using ScadaApp.Services;

namespace ScadaApp.ViewModels;

public partial class ChannelItemViewModel : ObservableObject
{
    private readonly IChannelManager _channelManager;

    [ObservableProperty] private ChannelState _state = ChannelState.Disconnected;
    [ObservableProperty] private bool _isRunning;

    public ChannelItemViewModel(IChannelManager channelManager, ChannelConfig config)
    {
        _channelManager = channelManager;
        Config = config;
    }

    public ChannelConfig Config { get; }

    public string Id => Config.Id;
    public string Name => Config.Name;
    public string PortName => Config.PortName;
    public int BaudRate => Config.BaudRate;
    public int TagCount => Config.Tags.Count;
    public string Summary => $"{PortName} @ {BaudRate} | {TagCount} 点";

    public void RefreshState()
    {
        var running = _channelManager.GetRunningChannel(Config.Id);
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
    }
}
