using ScadaApp.Models;

namespace ScadaApp.Services;

/// <summary>
/// 多通道管理器，统一管理所有 Modbus RTU 串口通道
/// </summary>
public sealed class ChannelManager : IChannelManager
{
    private readonly List<ChannelConfig> _channels = new();
    private readonly Dictionary<string, IChannelService> _running = new();

    public IReadOnlyList<ChannelConfig> Channels => _channels;
    public IReadOnlyList<IChannelService> RunningChannels => _running.Values.ToList();
    public event EventHandler? ChannelsChanged;
    public event EventHandler<LogEntry>? LogAdded;
    public event EventHandler<TagValue>? TagValueUpdated;
    public event EventHandler? ChannelStateChanged;

    public void AddChannel(ChannelConfig config)
    {
        _channels.Add(config);
        ChannelsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveChannel(string channelId)
    {
        _channels.RemoveAll(c => c.Id == channelId);
    }

    public async Task RemoveChannelAsync(string channelId)
    {
        if (_running.ContainsKey(channelId))
            await StopChannelAsync(channelId).ConfigureAwait(false);

        _channels.RemoveAll(c => c.Id == channelId);
    }

    public void UpdateChannel(ChannelConfig config)
    {
        var index = _channels.FindIndex(c => c.Id == config.Id);
        if (index >= 0)
            _channels[index] = config;
    }

    public async Task StartChannelAsync(string channelId, CancellationToken cancellationToken = default)
    {
        if (_running.ContainsKey(channelId))
            return;

        var config = _channels.FirstOrDefault(c => c.Id == channelId)
            ?? throw new ArgumentException($"未找到通道: {channelId}");

        if (!config.IsEnabled)
            throw new InvalidOperationException($"通道 {config.Name} 已禁用");

        var service = new ChannelService(config);
        service.LogAdded += (_, log) => LogAdded?.Invoke(this, log);
        service.TagValueUpdated += (_, value) => TagValueUpdated?.Invoke(this, value);
        service.StateChanged += (_, _) => ChannelStateChanged?.Invoke(this, EventArgs.Empty);

        _running[channelId] = service;
        try
        {
            await service.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _running.Remove(channelId);
            throw;
        }
    }

    public async Task StopChannelAsync(string channelId)
    {
        if (!_running.TryGetValue(channelId, out var service))
            return;

        await service.StopAsync().ConfigureAwait(false);
        if (service is IDisposable disposable)
            disposable.Dispose();
        _running.Remove(channelId);
    }

    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var channel in _channels.Where(c => c.IsEnabled))
        {
            try
            {
                await StartChannelAsync(channel.Id, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // 单通道失败不影响其他通道
            }
        }
    }

    public async Task StopAllAsync()
    {
        var ids = _running.Keys.ToList();
        foreach (var id in ids)
            await StopChannelAsync(id).ConfigureAwait(false);
    }

    public async Task WriteTagAsync(string channelId, string tagId, object value, CancellationToken cancellationToken = default)
    {
        if (!_running.TryGetValue(channelId, out var service))
            throw new InvalidOperationException("通道未运行");

        await service.WriteTagAsync(tagId, value, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteTagsAsync(
        string channelId,
        IReadOnlyList<(string TagId, object Value)> items,
        CancellationToken cancellationToken = default)
    {
        if (!_running.TryGetValue(channelId, out var service))
            throw new InvalidOperationException("通道未运行");

        await service.WriteTagsAsync(items, cancellationToken).ConfigureAwait(false);
    }

    public IChannelService? GetRunningChannel(string channelId)
    {
        _running.TryGetValue(channelId, out var service);
        return service;
    }
}
