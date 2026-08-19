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
    private readonly ConcurrentDictionary<string, string> _lastErrors = new();
    private ChannelState _state = ChannelState.Disconnected;
    private long _lastTx;
    private long _lastRx;

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
    public long TxCount => _client?.TxCount ?? _lastTx;
    public long RxCount => _client?.RxCount ?? _lastRx;
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
            // 连接成功后保持 Connected：轮询失败只记日志，不改回连接中/离线
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
            _lastTx = _client.TxCount;
            _lastRx = _client.RxCount;
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

    public async Task WriteTagsAsync(
        IReadOnlyList<(string TagId, object Value)> items,
        CancellationToken cancellationToken = default)
    {
        if (_client == null)
            throw new InvalidOperationException("通道未连接");

        if (items.Count == 0)
            return;

        var resolved = new List<(TagPoint Tag, object Value)>(items.Count);
        foreach (var (tagId, value) in items)
        {
            var tag = _config.Tags.FirstOrDefault(t => t.Id == tagId)
                ?? throw new ArgumentException($"未找到标签: {tagId}");
            resolved.Add((tag, value));
        }

        var ordered = resolved.OrderBy(i => i.Tag.Address).ToList();
        TagBlockWriter.EnsureWritableConsecutive(ordered.Select(i => i.Tag).ToList());

        foreach (var chunk in TagBlockWriter.SplitByPduLimit(ordered))
        {
            var registers = TagBlockWriter.EncodeBlock(chunk);
            var start = chunk[0].Tag.Address;
            await _client.WriteRegistersAsync(chunk[0].Tag.SlaveId, start, registers, cancellationToken)
                .ConfigureAwait(false);

            var names = string.Join("、", chunk.Select(i => $"{i.Tag.Name}={i.Value}"));
            AddLog("Info", $"功能码 16 连续写入 {chunk.Count} 点 @{start}：{names}");
        }

        var written = ordered.Select(i => i.Tag).ToList();
        var updated = await _client.ReadTagsAsync(written, cancellationToken).ConfigureAwait(false);
        foreach (var value in updated)
        {
            _tagValues[value.TagId] = value;
            TagValueUpdated?.Invoke(this, value);
        }
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var enabledTags = _config.Tags.Where(t => t.IsEnabled).ToList();
            if (enabledTags.Count > 0 && _client != null)
            {
                try
                {
                    var values = await _client.ReadTagsAsync(enabledTags, cancellationToken).ConfigureAwait(false);
                    foreach (var value in values)
                    {
                        _tagValues[value.TagId] = value;
                        TagValueUpdated?.Invoke(this, value);
                    }

                    ReportReadQuality(enabledTags, values);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AddLog("Error", $"块读取异常: {ex.Message}");
                }
            }

            var interval = Math.Max(100, _config.PollingIntervalMs);
            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ReportReadQuality(IReadOnlyList<TagPoint> tags, IReadOnlyList<TagValue> values)
    {
        var bad = values.Where(v => v.Quality == "Bad" && !string.IsNullOrEmpty(v.ErrorMessage)).ToList();
        if (bad.Count == 0)
        {
            foreach (var tag in tags)
                _lastErrors.TryRemove(tag.Id, out _);
            _lastErrors.TryRemove("__block__", out _);
            return;
        }

        if (bad.Count == values.Count)
        {
            var msg = bad[0].ErrorMessage!;
            if (_lastErrors.GetValueOrDefault("__block__") != msg)
            {
                _lastErrors["__block__"] = msg;
                AddLog("Warn", $"块读取失败: {msg}");
            }

            return;
        }

        _lastErrors.TryRemove("__block__", out _);
        foreach (var value in values)
        {
            if (value.Quality == "Bad" && value.ErrorMessage != null)
            {
                if (_lastErrors.GetValueOrDefault(value.TagId) != value.ErrorMessage)
                {
                    _lastErrors[value.TagId] = value.ErrorMessage;
                    var name = tags.FirstOrDefault(t => t.Id == value.TagId)?.Name ?? value.TagId;
                    AddLog("Warn", $"{name}: {value.ErrorMessage}");
                }
            }
            else
            {
                _lastErrors.TryRemove(value.TagId, out _);
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
        try
        {
            _pollCts?.Cancel();
        }
        catch
        {
            // ignored
        }

        _client?.Dispose();
        _pollCts?.Dispose();
    }
}
