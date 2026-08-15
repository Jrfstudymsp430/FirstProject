using System.Collections.Concurrent;

namespace ScadaApp.Services;

/// <summary>
/// 按点位 Id 复用历史缓冲，避免切换通道或刷新点表时曲线被清空。
/// </summary>
public sealed class TrendStore
{
    private readonly ConcurrentDictionary<string, TrendBuffer> _buffers = new();

    public TrendBuffer GetOrCreate(string tagId) =>
        _buffers.GetOrAdd(tagId, _ => new TrendBuffer());

    public void Remove(string tagId) => _buffers.TryRemove(tagId, out _);
}
