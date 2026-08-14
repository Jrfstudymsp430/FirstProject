using ScadaApp.Models;

namespace ScadaApp.Services;

public interface IChannelService
{
    ChannelConfig Config { get; }
    ChannelState State { get; }
    IReadOnlyDictionary<string, TagValue> TagValues { get; }
    event EventHandler? StateChanged;
    event EventHandler<TagValue>? TagValueUpdated;
    event EventHandler<LogEntry>? LogAdded;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    Task WriteTagAsync(string tagId, object value, CancellationToken cancellationToken = default);
}
