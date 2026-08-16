using ScadaApp.Models;

namespace ScadaApp.Services;

public interface IChannelManager
{
    IReadOnlyList<ChannelConfig> Channels { get; }
    IReadOnlyList<IChannelService> RunningChannels { get; }
    event EventHandler? ChannelsChanged;
    event EventHandler<LogEntry>? LogAdded;
    event EventHandler<TagValue>? TagValueUpdated;
    event EventHandler? ChannelStateChanged;

    void AddChannel(ChannelConfig config);
    void RemoveChannel(string channelId);
    Task RemoveChannelAsync(string channelId);
    void UpdateChannel(ChannelConfig config);
    Task StartChannelAsync(string channelId, CancellationToken cancellationToken = default);
    Task StopChannelAsync(string channelId);
    Task StartAllAsync(CancellationToken cancellationToken = default);
    Task StopAllAsync();
    Task WriteTagAsync(string channelId, string tagId, object value, CancellationToken cancellationToken = default);
    Task WriteTagsAsync(string channelId, IReadOnlyList<(string TagId, object Value)> items, CancellationToken cancellationToken = default);
    IChannelService? GetRunningChannel(string channelId);
    (long TxCount, long RxCount) GetTraffic(string channelId);
}
