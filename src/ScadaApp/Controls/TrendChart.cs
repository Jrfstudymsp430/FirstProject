using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ScadaApp.Services;

namespace ScadaApp.Controls;

/// <summary>
/// 自绘实时曲线：环形缓冲快照后按像素列做 min/max 降采样，避免 LiveCharts 一类可视化树开销。
/// </summary>
public sealed class TrendChart : FrameworkElement
{
    public static readonly DependencyProperty BufferProperty = DependencyProperty.Register(
        nameof(Buffer), typeof(TrendBuffer), typeof(TrendChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnWatchPropertyChanged));

    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(TrendChart),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WindowSecondsProperty = DependencyProperty.Register(
        nameof(WindowSeconds), typeof(double), typeof(TrendChart),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnWatchPropertyChanged));

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush), typeof(Brush), typeof(TrendChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ClickCommandProperty = DependencyProperty.Register(
        nameof(ClickCommand), typeof(ICommand), typeof(TrendChart));

    public static readonly DependencyProperty ClickCommandParameterProperty = DependencyProperty.Register(
        nameof(ClickCommandParameter), typeof(object), typeof(TrendChart));

    private static readonly DispatcherTimer SharedTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private static readonly List<WeakReference<TrendChart>> ActiveCharts = new();

    private double[] _values = new double[TrendBuffer.DefaultCapacity];
    private long[] _ticks = new long[TrendBuffer.DefaultCapacity];
    private readonly double[] _colMin = new double[4096];
    private readonly double[] _colMax = new double[4096];
    private readonly double[] _colLast = new double[4096];
    private readonly bool[] _colHas = new bool[4096];
    private int _lastVersion = -1;
    private int _hoverX = -1;
    private string? _hoverText;

    static TrendChart()
    {
        ClipToBoundsProperty.OverrideMetadata(typeof(TrendChart), new FrameworkPropertyMetadata(true));
        SnapsToDevicePixelsProperty.OverrideMetadata(typeof(TrendChart), new FrameworkPropertyMetadata(true));
        SharedTimer.Tick += (_, _) =>
        {
            for (var i = ActiveCharts.Count - 1; i >= 0; i--)
            {
                if (!ActiveCharts[i].TryGetTarget(out var chart))
                {
                    ActiveCharts.RemoveAt(i);
                    continue;
                }

                var version = chart.Buffer?.Version ?? -1;
                if (version != chart._lastVersion)
                    chart.InvalidateVisual();
            }

            if (ActiveCharts.Count == 0)
                SharedTimer.Stop();
        };
    }

    public TrendChart()
    {
        Loaded += (_, _) =>
        {
            ActiveCharts.Add(new WeakReference<TrendChart>(this));
            if (!SharedTimer.IsEnabled)
                SharedTimer.Start();
        };
        Unloaded += (_, _) =>
        {
            for (var i = ActiveCharts.Count - 1; i >= 0; i--)
            {
                if (!ActiveCharts[i].TryGetTarget(out var chart) || ReferenceEquals(chart, this))
                    ActiveCharts.RemoveAt(i);
            }

            if (ActiveCharts.Count == 0)
                SharedTimer.Stop();
        };
        SizeChanged += (_, _) => InvalidateVisual();
        MouseMove += OnMouseMove;
        MouseLeave += (_, _) =>
        {
            if (_hoverX < 0)
                return;
            _hoverX = -1;
            _hoverText = null;
            InvalidateVisual();
        };
    }

    public TrendBuffer? Buffer
    {
        get => (TrendBuffer?)GetValue(BufferProperty);
        set => SetValue(BufferProperty, value);
    }

    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    public double WindowSeconds
    {
        get => (double)GetValue(WindowSecondsProperty);
        set => SetValue(WindowSecondsProperty, value);
    }

    public Brush? LineBrush
    {
        get => (Brush?)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public ICommand? ClickCommand
    {
        get => (ICommand?)GetValue(ClickCommandProperty);
        set => SetValue(ClickCommandProperty, value);
    }

    public object? ClickCommandParameter
    {
        get => GetValue(ClickCommandParameterProperty);
        set => SetValue(ClickCommandParameterProperty, value);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!Compact || e.Handled)
            return;
        if (ClickCommand?.CanExecute(ClickCommandParameter) == true)
        {
            ClickCommand.Execute(ClickCommandParameter);
            e.Handled = true;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = (int)Math.Ceiling(ActualWidth);
        var height = (int)Math.Ceiling(ActualHeight);
        if (width < 8 || height < 8)
            return;

        var buffer = Buffer;
        _lastVersion = buffer?.Version ?? -1;
        var count = buffer?.CopyTo(_values, _ticks) ?? 0;
        if (count < 1)
        {
            DrawEmpty(dc, width, height);
            return;
        }

        var windowTicks = WindowSeconds > 0 ? TimeSpan.FromSeconds(WindowSeconds).Ticks : 0L;
        var tEnd = _ticks[count - 1];
        var tStart = windowTicks > 0 ? tEnd - windowTicks : _ticks[0];
        if (tEnd <= tStart)
            tStart = tEnd - TimeSpan.FromSeconds(1).Ticks;

        var startIndex = 0;
        if (windowTicks > 0)
        {
            while (startIndex < count && _ticks[startIndex] < tStart)
                startIndex++;
            if (startIndex >= count)
            {
                DrawEmpty(dc, width, height);
                return;
            }

            if (startIndex > 0)
                startIndex--;
        }

        var compact = Compact;
        var padL = compact ? 2d : 52d;
        var padR = compact ? 2d : 12d;
        var padT = compact ? 4d : 12d;
        var padB = compact ? 4d : 24d;
        var plotW = Math.Max(4, width - padL - padR);
        var plotH = Math.Max(4, height - padT - padB);
        var colCount = Math.Min(_colHas.Length, Math.Max(8, (int)plotW));

        Array.Clear(_colHas, 0, colCount);
        var span = (double)(tEnd - tStart);
        if (span <= 0)
            span = 1;

        for (var i = startIndex; i < count; i++)
        {
            var x = (_ticks[i] - tStart) / span;
            if (x < 0) x = 0;
            if (x > 1) x = 1;
            var col = Math.Min(colCount - 1, (int)(x * (colCount - 1)));
            var v = _values[i];
            if (!_colHas[col])
            {
                _colMin[col] = v;
                _colMax[col] = v;
                _colHas[col] = true;
            }
            else
            {
                if (v < _colMin[col]) _colMin[col] = v;
                if (v > _colMax[col]) _colMax[col] = v;
            }

            _colLast[col] = v;
        }

        double yMin = double.MaxValue, yMax = double.MinValue;
        var hasCol = false;
        for (var c = 0; c < colCount; c++)
        {
            if (!_colHas[c])
                continue;
            hasCol = true;
            if (_colMin[c] < yMin) yMin = _colMin[c];
            if (_colMax[c] > yMax) yMax = _colMax[c];
        }

        if (!hasCol)
        {
            DrawEmpty(dc, width, height);
            return;
        }

        if (Math.Abs(yMax - yMin) < 1e-12)
        {
            yMin -= 1;
            yMax += 1;
        }
        else
        {
            var pad = (yMax - yMin) * 0.08;
            yMin -= pad;
            yMax += pad;
        }

        var plot = new Rect(padL, padT, plotW, plotH);
        if (!compact)
            DrawGrid(dc, plot, tStart, tEnd, yMin, yMax);

        var line = (LineBrush as SolidColorBrush)?.Color ?? Color.FromRgb(0x2E, 0xE6, 0xC0);
        DrawSeries(dc, plot, colCount, yMin, yMax, line, compact);

        if (!compact && _hoverX >= 0)
            DrawHover(dc, plot, colCount, tStart, tEnd, yMin, yMax, line);
    }

    private void DrawSeries(DrawingContext dc, Rect plot, int colCount, double yMin, double yMax, Color line, bool compact)
    {
        var fill = new SolidColorBrush(Color.FromArgb(compact ? (byte)0x28 : (byte)0x40, line.R, line.G, line.B));
        fill.Freeze();
        var stroke = new Pen(new SolidColorBrush(line), compact ? 1.2 : 1.6) { LineJoin = PenLineJoin.Round };
        stroke.Brush.Freeze();
        stroke.Freeze();
        var envelope = new Pen(new SolidColorBrush(Color.FromArgb(0x66, line.R, line.G, line.B)), 1);
        envelope.Brush.Freeze();
        envelope.Freeze();

        var geo = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var ctx = geo.Open())
        {
            var started = false;
            Point first = default, last = default;
            for (var c = 0; c < colCount; c++)
            {
                if (!_colHas[c])
                    continue;

                var pt = ToPoint(plot, c, colCount, _colLast[c], yMin, yMax);
                if (!started)
                {
                    ctx.BeginFigure(pt, true, true);
                    first = last = pt;
                    started = true;
                }
                else
                {
                    ctx.LineTo(pt, true, false);
                    last = pt;
                }
            }

            if (started)
            {
                ctx.LineTo(new Point(last.X, plot.Bottom), true, false);
                ctx.LineTo(new Point(first.X, plot.Bottom), true, false);
            }
        }

        geo.Freeze();
        dc.DrawGeometry(fill, null, geo);

        var lineGeo = new StreamGeometry();
        using (var ctx = lineGeo.Open())
        {
            var started = false;
            for (var c = 0; c < colCount; c++)
            {
                if (!_colHas[c])
                    continue;

                var lastPt = ToPoint(plot, c, colCount, _colLast[c], yMin, yMax);
                if (!started)
                {
                    ctx.BeginFigure(lastPt, false, false);
                    started = true;
                }
                else
                {
                    ctx.LineTo(lastPt, true, false);
                }

                if (!compact && _colMax[c] - _colMin[c] > (yMax - yMin) * 0.002)
                {
                    var p0 = ToPoint(plot, c, colCount, _colMin[c], yMin, yMax);
                    var p1 = ToPoint(plot, c, colCount, _colMax[c], yMin, yMax);
                    dc.DrawLine(envelope, p0, p1);
                }
            }
        }

        lineGeo.Freeze();
        dc.DrawGeometry(null, stroke, lineGeo);
    }

    private void DrawGrid(DrawingContext dc, Rect plot, long tStart, long tEnd, double yMin, double yMax)
    {
        var border = new Pen(new SolidColorBrush(Color.FromRgb(0x35, 0x48, 0x5E)), 1);
        border.Brush.Freeze();
        border.Freeze();
        var grid = new Pen(new SolidColorBrush(Color.FromArgb(0x55, 0x35, 0x48, 0x5E)), 1);
        grid.Brush.Freeze();
        grid.Freeze();

        dc.DrawRectangle(null, border, plot);
        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var textBrush = new SolidColorBrush(Color.FromRgb(0xA8, 0xB6, 0xC6));
        textBrush.Freeze();

        for (var i = 0; i <= 4; i++)
        {
            var y = plot.Top + plot.Height * i / 4;
            if (i is > 0 and < 4)
                dc.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));

            var value = yMax - (yMax - yMin) * i / 4;
            var label = FormatAxis(value);
            var ft = new FormattedText(
                label,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                11,
                textBrush,
                dip);
            dc.DrawText(ft, new Point(plot.Left - ft.Width - 6, y - ft.Height / 2));
        }

        for (var i = 0; i <= 4; i++)
        {
            var x = plot.Left + plot.Width * i / 4;
            if (i is > 0 and < 4)
                dc.DrawLine(grid, new Point(x, plot.Top), new Point(x, plot.Bottom));

            var tick = tStart + (long)((tEnd - tStart) * (i / 4d));
            var label = new DateTime(tick).ToString("HH:mm:ss");
            var ft = new FormattedText(
                label,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Consolas"),
                11,
                textBrush,
                dip);
            dc.DrawText(ft, new Point(x - ft.Width / 2, plot.Bottom + 4));
        }
    }

    private void DrawHover(
        DrawingContext dc,
        Rect plot,
        int colCount,
        long tStart,
        long tEnd,
        double yMin,
        double yMax,
        Color line)
    {
        if (_hoverX < plot.Left || _hoverX > plot.Right)
            return;

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(0xAA, 0x4E, 0xC9, 0xF5)), 1);
        pen.Brush.Freeze();
        pen.Freeze();
        dc.DrawLine(pen, new Point(_hoverX, plot.Top), new Point(_hoverX, plot.Bottom));

        if (string.IsNullOrEmpty(_hoverText))
            return;

        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var ft = new FormattedText(
            _hoverText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            12,
            Brushes.White,
            dip);
        var box = new Rect(
            Math.Min(_hoverX + 8, plot.Right - ft.Width - 12),
            plot.Top + 6,
            ft.Width + 12,
            ft.Height + 8);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xCC, 0x1E, 0x2B, 0x3E)),
            new Pen(new SolidColorBrush(line), 1), box);
        dc.DrawText(ft, new Point(box.X + 6, box.Y + 4));
    }

    private void DrawEmpty(DrawingContext dc, int width, int height)
    {
        if (Compact)
            return;

        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var ft = new FormattedText(
            "暂无历史数据，启动通道后将自动记录",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"),
            13,
            new SolidColorBrush(Color.FromRgb(0xA8, 0xB6, 0xC6)),
            dip);
        dc.DrawText(ft, new Point((width - ft.Width) / 2, (height - ft.Height) / 2));
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (Compact)
            return;

        var pos = e.GetPosition(this);
        var padL = 52d;
        var padR = 12d;
        var plotW = Math.Max(4, ActualWidth - padL - padR);
        if (pos.X < padL || pos.X > padL + plotW)
        {
            if (_hoverX >= 0)
            {
                _hoverX = -1;
                _hoverText = null;
                InvalidateVisual();
            }
            return;
        }

        var buffer = Buffer;
        var count = buffer?.CopyTo(_values, _ticks) ?? 0;
        if (count < 1)
            return;

        var windowTicks = WindowSeconds > 0 ? TimeSpan.FromSeconds(WindowSeconds).Ticks : 0L;
        var tEnd = _ticks[count - 1];
        var tStart = windowTicks > 0 ? tEnd - windowTicks : _ticks[0];
        var ratio = (pos.X - padL) / plotW;
        var target = tStart + (long)((tEnd - tStart) * Math.Clamp(ratio, 0, 1));

        var best = 0;
        var bestDelta = long.MaxValue;
        for (var i = 0; i < count; i++)
        {
            if (windowTicks > 0 && _ticks[i] < tStart)
                continue;
            var d = Math.Abs(_ticks[i] - target);
            if (d < bestDelta)
            {
                bestDelta = d;
                best = i;
            }
        }

        _hoverX = (int)pos.X;
        _hoverText = $"{new DateTime(_ticks[best]):HH:mm:ss.fff}  {_values[best]:G6}";
        InvalidateVisual();
    }

    private static Point ToPoint(Rect plot, int col, int colCount, double value, double yMin, double yMax)
    {
        var x = plot.Left + (colCount <= 1 ? 0 : plot.Width * col / (colCount - 1));
        var y = plot.Bottom - (value - yMin) / (yMax - yMin) * plot.Height;
        if (y < plot.Top) y = plot.Top;
        if (y > plot.Bottom) y = plot.Bottom;
        return new Point(x, y);
    }

    private static string FormatAxis(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1000) return value.ToString("0");
        if (abs >= 10) return value.ToString("0.0");
        if (abs >= 1) return value.ToString("0.00");
        return value.ToString("0.000");
    }

    private static void OnWatchPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrendChart chart)
            chart.InvalidateVisual();
    }
}
