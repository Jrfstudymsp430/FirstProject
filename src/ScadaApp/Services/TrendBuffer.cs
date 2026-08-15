namespace ScadaApp.Services;

/// <summary>
/// 定长环形缓冲，按时间顺序保存点位数值。拷贝为线性快照，供自绘曲线降采样。
/// </summary>
public sealed class TrendBuffer
{
    public const int DefaultCapacity = 16384;

    private readonly double[] _values;
    private readonly long[] _ticks;
    private readonly object _sync = new();
    private int _head;
    private int _count;
    private int _version;

    public TrendBuffer(int capacity = DefaultCapacity)
    {
        Capacity = Math.Max(32, capacity);
        _values = new double[Capacity];
        _ticks = new long[Capacity];
    }

    public int Capacity { get; }
    public int Count
    {
        get { lock (_sync) return _count; }
    }

    public int Version
    {
        get { lock (_sync) return _version; }
    }

    public void Add(DateTime timestamp, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return;

        var tick = timestamp.Ticks;
        lock (_sync)
        {
            if (_count > 0)
            {
                var lastIndex = (_head + _count - 1) % Capacity;
                if (_ticks[lastIndex] == tick && Math.Abs(_values[lastIndex] - value) < double.Epsilon)
                    return;
            }

            int write;
            if (_count < Capacity)
            {
                write = (_head + _count) % Capacity;
                _count++;
            }
            else
            {
                write = _head;
                _head = (_head + 1) % Capacity;
            }

            _values[write] = value;
            _ticks[write] = tick;
            _version++;
        }
    }

    public int CopyTo(double[] values, long[] ticks)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(ticks);

        lock (_sync)
        {
            var n = Math.Min(_count, Math.Min(values.Length, ticks.Length));
            if (n == 0)
                return 0;

            var first = Capacity - _head;
            if (first >= n)
            {
                Array.Copy(_values, _head, values, 0, n);
                Array.Copy(_ticks, _head, ticks, 0, n);
            }
            else
            {
                Array.Copy(_values, _head, values, 0, first);
                Array.Copy(_ticks, _head, ticks, 0, first);
                Array.Copy(_values, 0, values, first, n - first);
                Array.Copy(_ticks, 0, ticks, first, n - first);
            }

            return n;
        }
    }

    public bool TryGetStats(TimeSpan window, out double min, out double max, out double avg, out double last, out int count)
    {
        min = max = avg = last = 0;
        count = 0;

        lock (_sync)
        {
            if (_count == 0)
                return false;

            var cutoff = window <= TimeSpan.Zero
                ? 0L
                : _ticks[(_head + _count - 1) % Capacity] - window.Ticks;

            double sum = 0;
            var n = 0;
            var started = false;
            for (var i = 0; i < _count; i++)
            {
                var idx = (_head + i) % Capacity;
                if (_ticks[idx] < cutoff)
                    continue;

                var v = _values[idx];
                if (!started)
                {
                    min = max = v;
                    started = true;
                }
                else
                {
                    if (v < min) min = v;
                    if (v > max) max = v;
                }

                sum += v;
                last = v;
                n++;
            }

            if (n == 0)
                return false;

            avg = sum / n;
            count = n;
            return true;
        }
    }
}
