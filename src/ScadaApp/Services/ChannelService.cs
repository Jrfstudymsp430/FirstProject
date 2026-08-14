using System.Collections.Concurrent;
using ScadaApp.Models;

namespace ScadaApp.Services;

/// <summary>
/// 管理单个串口通道的轮询与数据采集
/// </summary>
public sealed class ChannelService : IChannelService, IDisposable
{
    private readonly ChannelConfig _config;
    private ModbusRtuClient? _client;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;
    private readonly ConcurrentDictionary<string, TagValue> _tagValues = new();
    private ChannelState _state = ChannelState.Disconnected;

    public ChannelService(ChannelConfig config)
    {
        _config = config;
        foreach (var tag in _config.Tags)
        {
            _tagValues[tag.Id] = new TagValue { TagId = tag.Id, Quality = "Bad", DisplayValue = "--" };
        }
    }

    public ChannelConfig Config => _config;
    public ChannelState State => _state;
    public IReadOnlyDictionary<string, TagValue> TagValues => _tagValues;
    public event EventHandler? StateChanged;
    public event EventHandler<TagValue>? TagValueUpdated;
    public event EventHandler<LogEntry>? LogAdded;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_pollTask != null)
            return;

        SetState(ChannelState.Connecting);
        AddLog("Info", $"正在连接 {_config.PortName}...");

        try
        {
            _client = new ModbusRtuClient(_config);
            await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);
            SetState(ChannelState.Connected);
            AddLog("Info", $"通道已连接: {_config.PortName} @ {_config.BaudRate}");

            _pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pollTask = Task.Run(() => PollLoopAsync(_pollCts.Token), _pollCts.Token);
        }
        catch (Exception ex)
        {
            SetState(ChannelState.Error);
            AddLog("Error", $"连接失败: {ex.Message}");
            await StopAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_pollCts != null)
        {
            await _pollCts.CancelAsync().ConfigureAwait(false);
            if (_pollTask != null)
            {
                try { await _pollTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            _pollCts.Dispose();
            _pollCts = null;
            _pollTask = null;
        }

        if (_client != null)
        {
            await _client.DisconnectAsync().ConfigureAwait(false);
            _client.Dispose();
            _client = null;
        }

        SetState(ChannelState.Disconnected);
        AddLog("Info", "通道已停止");
    }

    public async Task WriteTagAsync(string tagId, object value, CancellationToken cancellationToken = default)
    {
        var tag = _config.Tags.FirstOrDefault(t => t.Id == tagId)
            ?? throw new ArgumentException($"未找到标签: {tagId}");

        if (_client == null)
            throw new InvalidOperationException("通道未连接");

        await _client.WriteTagAsync(tag, value, cancellationToken).ConfigureAwait(false);
        AddLog("Info", $"写入 {tag.Name} = {value}");

        var updated = await _client.ReadTagAsync(tag, cancellationToken).ConfigureAwait(false);
        _tagValues[tagId] = updated;
        TagValueUpdated?.Invoke(this, updated);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var enabledTags = _config.Tags.Where(t => t.IsEnabled).ToList();
            foreach (var tag in enabledTags)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (_client == null)
                    continue;

                try
                {
                    var value = await _client.ReadTagAsync(tag, cancellationToken).ConfigureAwait(false);
                    _tagValues[tag.Id] = value;
                    TagValueUpdated?.Invoke(this, value);

                    if (value.Quality == "Bad" && value.ErrorMessage != null)
                        AddLog("Warn", $"{tag.Name}: {value.ErrorMessage}");
                }
                catch (Exception ex)
                {
                    AddLog("Error", $"{tag.Name} 读取异常: {ex.Message}");
                }
            }

            try
            {
                await Task.Delay(_config.PollingIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void SetState(ChannelState state)
    {
        _state = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddLog(string level, string message)
    {
        LogAdded?.Invoke(this, new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            ChannelName = _config.Name,
            Message = message
        });
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}
