using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScadaApp.Models;
using ScadaApp.Services;
using ScadaApp.Views;

namespace ScadaApp.ViewModels;

public sealed record CalibrationWriteModeOption(CalibrationWriteMode Mode, string Name);

public partial class CalibrationPointItem : ObservableObject
{
    [ObservableProperty] private int _ordinal;
    [ObservableProperty] private double _measured;
    [ObservableProperty] private double _standard;

    public string MeasuredText => CalibrationViewModel.FormatValue(Measured);
    public string StandardText => CalibrationViewModel.FormatValue(Standard);

    partial void OnMeasuredChanged(double value) => OnPropertyChanged(nameof(MeasuredText));
    partial void OnStandardChanged(double value) => OnPropertyChanged(nameof(StandardText));
}

public partial class CalibrationSampleItem : ObservableObject
{
    [ObservableProperty] private int _ordinal;
    [ObservableProperty] private DateTime _timestamp;
    [ObservableProperty] private double _value;

    public string TimeText => Timestamp == default
        ? "--"
        : Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    public string ValueText => CalibrationViewModel.FormatValue(Value);

    partial void OnTimestampChanged(DateTime value) => OnPropertyChanged(nameof(TimeText));
    partial void OnValueChanged(double value) => OnPropertyChanged(nameof(ValueText));
}

public partial class CalibrationViewModel : ObservableObject
{
    private readonly IChannelManager _channelManager;
    private readonly double[] _seedValues = new double[50];
    private readonly long[] _seedTicks = new long[50];
    private ChannelConfig _config;
    private DateTime _lastSampleTime;
    private string? _lastSampleTagId;
    private bool _loading;
    private bool _seeded;

    [ObservableProperty] private TagItemViewModel? _selectedTag;
    [ObservableProperty] private int _sampleCount = 8;
    [ObservableProperty] private string _currentValueText = "--";
    [ObservableProperty] private string _averageText = "--";
    [ObservableProperty] private string _sampleHint = "等待有效采集";
    [ObservableProperty] private string _standardInput = string.Empty;
    [ObservableProperty] private string _startAddressText = "0";
    [ObservableProperty] private TagDataType _downloadDataType = TagDataType.Float32;
    [ObservableProperty] private CalibrationWriteMode _writeMode = CalibrationWriteMode.Table;
    [ObservableProperty] private string _slopeText = "--";
    [ObservableProperty] private string _interceptText = "--";
    [ObservableProperty] private string _fitHint = "至少 2 个标定点后显示拟合";
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private string _title = "传感器标定";

    public CalibrationViewModel(IChannelManager channelManager, ChannelConfig config)
    {
        _channelManager = channelManager;
        _config = config;
        Tags = new ObservableCollection<TagItemViewModel>();
        RecentSamples = new ObservableCollection<CalibrationSampleItem>();
        Points = new ObservableCollection<CalibrationPointItem>();
        SampleCounts = new[] { 5, 8, 10, 15, 20 };
        DownloadTypes = new[]
        {
            TagDataType.Float32,
            TagDataType.Double64,
            TagDataType.Int32,
            TagDataType.UInt16
        };
        WriteModes = new[]
        {
            new CalibrationWriteModeOption(CalibrationWriteMode.Table, "标定表（实测、标准交替）"),
            new CalibrationWriteModeOption(CalibrationWriteMode.SlopeIntercept, "斜率与截距（k、b）")
        };
    }

    public ObservableCollection<TagItemViewModel> Tags { get; }
    public ObservableCollection<CalibrationSampleItem> RecentSamples { get; }
    public ObservableCollection<CalibrationPointItem> Points { get; }
    public int[] SampleCounts { get; }
    public TagDataType[] DownloadTypes { get; }
    public CalibrationWriteModeOption[] WriteModes { get; }
    public string ChannelId => _config.Id;
    public ChannelConfig Config => _config;

    public void BindTags(IEnumerable<TagItemViewModel> tags, ChannelConfig config)
    {
        _loading = true;
        _config = config;
        Title = $"传感器标定 · {config.Name}";

        Tags.Clear();
        foreach (var tag in tags)
            Tags.Add(tag);

        SampleCount = config.CalibrationSampleCount is >= 3 and <= 50
            ? config.CalibrationSampleCount
            : 8;
        StartAddressText = config.CalibrationStartAddress.ToString(CultureInfo.InvariantCulture);
        DownloadDataType = Enum.IsDefined(config.CalibrationDataType)
            ? config.CalibrationDataType
            : TagDataType.Float32;
        WriteMode = Enum.IsDefined(config.CalibrationWriteMode)
            ? config.CalibrationWriteMode
            : CalibrationWriteMode.Table;

        Points.Clear();
        foreach (var point in config.CalibrationPoints ?? new List<CalibrationPoint>())
        {
            Points.Add(new CalibrationPointItem
            {
                Measured = point.Measured,
                Standard = point.Standard
            });
        }

        Renumber();
        UpdateFit();

        var selected = Tags.FirstOrDefault(t => t.TagId == config.CalibrationTagId)
            ?? Tags.FirstOrDefault();
        if (!ReferenceEquals(SelectedTag, selected))
            SelectedTag = selected;
        else
            ResetLiveBuffer();

        _loading = false;
        RefreshLive();
    }

    public void RefreshLive()
    {
        var tag = SelectedTag;
        if (tag == null)
        {
            CurrentValueText = "--";
            AverageText = "--";
            SampleHint = Tags.Count == 0 ? "请先在点表中添加采集点" : "请选择实测点位";
            if (RecentSamples.Count > 0)
                RecentSamples.Clear();
            return;
        }

        CurrentValueText = string.IsNullOrWhiteSpace(tag.DisplayValue) ? "--" : tag.DisplayValue;
        TryAppendSample(tag);
        UpdateAverageText();
    }

    public void Persist()
    {
        if (_loading)
            return;

        _config.CalibrationTagId = SelectedTag?.TagId;
        _config.CalibrationSampleCount = SampleCount;
        if (TryParseAddress(StartAddressText, out var address))
            _config.CalibrationStartAddress = address;
        _config.CalibrationDataType = DownloadDataType;
        _config.CalibrationWriteMode = WriteMode;
        _config.CalibrationPoints = Points
            .Select(p => new CalibrationPoint { Measured = p.Measured, Standard = p.Standard })
            .ToList();
        _channelManager.UpdateChannel(_config);
        ConfigStorage.Save(_channelManager.Channels);
    }

    [RelayCommand]
    private void AddPoint()
    {
        if (!TryGetAverage(out var measured, out var count) || count == 0)
        {
            MessageDialog.Alert("传感器标定", "还没有有效的实测均值。请先启动通道，等采集稳定后再添加。");
            return;
        }

        if (!TryParseNumber(StandardInput, out var standard))
        {
            MessageDialog.Alert("传感器标定", "请输入有效的标准值。");
            return;
        }

        Points.Add(new CalibrationPointItem
        {
            Measured = measured,
            Standard = standard
        });
        StandardInput = string.Empty;
        Renumber();
        UpdateFit();
        Persist();
        StatusText = $"已添加第 {Points.Count} 个标定点（实测 {FormatValue(measured)}）";
    }

    [RelayCommand]
    private void RemovePoint(CalibrationPointItem? point)
    {
        if (point == null)
            return;
        Points.Remove(point);
        Renumber();
        UpdateFit();
        Persist();
        StatusText = "已删除标定点";
    }

    [RelayCommand]
    private void ClearPoints()
    {
        if (Points.Count == 0)
            return;
        if (!MessageDialog.Confirm("清空标定点", "确定清空当前通道的全部标定点？"))
            return;

        Points.Clear();
        UpdateFit();
        Persist();
        StatusText = "已清空标定点";
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (IsDownloading)
            return;

        var running = _channelManager.GetRunningChannel(_config.Id);
        if (running == null || running.State != ChannelState.Connected)
        {
            MessageDialog.Alert("下载标定", "请先启动该通道，再把标定数据写入下位机。");
            return;
        }

        if (!TryParseAddress(StartAddressText, out var address))
        {
            MessageDialog.Alert("下载标定", "起始地址必须是 0–65535 的整数。");
            return;
        }
        ushort[] registers;
        string summary;
        try
        {
            (registers, summary) = BuildDownload(address);
        }
        catch (Exception ex)
        {
            MessageDialog.Alert("下载标定", ex.Message);
            return;
        }

        if (!MessageDialog.Confirm(
                "下载标定",
                $"将用功能码 16 写入从机 {_config.SlaveId}，起始地址 {address}，共 {registers.Length} 个寄存器。\n{summary}\n\n确定下载？"))
            return;

        IsDownloading = true;
        StatusText = "正在下载…";
        try
        {
            await _channelManager.WriteRegistersAsync(_config.Id, address, registers);
            Persist();
            StatusText = $"已下载 {registers.Length} 个寄存器到地址 {address}";
        }
        catch (Exception ex)
        {
            StatusText = $"下载失败: {ex.Message}";
            MessageDialog.Alert("下载失败", ex.Message);
        }
        finally
        {
            IsDownloading = false;
        }
    }

    partial void OnSelectedTagChanged(TagItemViewModel? value)
    {
        ResetLiveBuffer();
        if (!_loading)
            Persist();
        RefreshLive();
    }

    partial void OnSampleCountChanged(int value)
    {
        TrimRecent();
        RenumberRecent();
        UpdateAverageText();
        if (!_loading)
            Persist();
        RefreshLive();
    }

    partial void OnStartAddressTextChanged(string value)
    {
        if (!_loading)
            Persist();
    }

    partial void OnDownloadDataTypeChanged(TagDataType value)
    {
        if (!_loading)
            Persist();
    }

    partial void OnWriteModeChanged(CalibrationWriteMode value)
    {
        if (!_loading)
            Persist();
        UpdateFit();
    }

    private (ushort[] Registers, string Summary) BuildDownload(ushort address)
    {
        if (WriteMode == CalibrationWriteMode.SlopeIntercept)
        {
            var models = ToModels();
            if (!CalibrationEncoder.TryFitLine(models, out var slope, out var intercept))
                throw new InvalidOperationException("斜率截距至少需要 2 个实测值不同的标定点。");

            var registers = CalibrationEncoder.EncodeSlopeIntercept(slope, intercept, DownloadDataType);
            var summary = $"内容：斜率 k = {FormatValue(slope)}，截距 b = {FormatValue(intercept)}（标准 = k × 实测 + b）";
            return (registers, summary);
        }

        if (Points.Count == 0)
            throw new InvalidOperationException("请至少添加 1 个标定点。");

        var table = CalibrationEncoder.EncodeTable(ToModels(), DownloadDataType);
        var summaryTable = $"内容：标定表 {Points.Count} 组，每组先写实测均值再写标准值";
        _ = address;
        return (table, summaryTable);
    }

    private List<CalibrationPoint> ToModels() =>
        Points.Select(p => new CalibrationPoint { Measured = p.Measured, Standard = p.Standard }).ToList();

    private bool TryGetAverage(out double average, out int count)
    {
        average = 0;
        count = RecentSamples.Count;
        if (count == 0)
            return false;
        average = RecentSamples.Average(s => s.Value);
        return true;
    }

    private void UpdateAverageText()
    {
        if (RecentSamples.Count == 0)
        {
            AverageText = "--";
            SampleHint = "等待有效采集";
            return;
        }

        AverageText = FormatValue(RecentSamples.Average(s => s.Value));
        SampleHint = RecentSamples.Count >= SampleCount
            ? $"下列 {SampleCount} 点均值"
            : $"已采集 {RecentSamples.Count}/{SampleCount} 点";
    }

    private void TryAppendSample(TagItemViewModel tag)
    {
        if (!_seeded)
        {
            SeedFromTrend(tag);
            _seeded = true;
        }

        if (tag.Quality != "Good" || tag.NumericValue is not double number)
            return;
        if (double.IsNaN(number) || double.IsInfinity(number))
            return;

        var stamp = tag.Timestamp == default ? DateTime.Now : tag.Timestamp;
        if (_lastSampleTagId == tag.TagId && stamp == _lastSampleTime)
            return;

        _lastSampleTagId = tag.TagId;
        _lastSampleTime = stamp;
        RecentSamples.Add(new CalibrationSampleItem
        {
            Timestamp = stamp,
            Value = number
        });
        TrimRecent();
        RenumberRecent();
    }

    private void SeedFromTrend(TagItemViewModel tag)
    {
        var n = tag.Trend.CopyRecent(SampleCount, _seedValues, _seedTicks);
        RecentSamples.Clear();
        for (var i = 0; i < n; i++)
        {
            RecentSamples.Add(new CalibrationSampleItem
            {
                Timestamp = _seedTicks[i] == 0 ? default : new DateTime(_seedTicks[i]),
                Value = _seedValues[i]
            });
        }

        if (n > 0)
        {
            _lastSampleTagId = tag.TagId;
            _lastSampleTime = RecentSamples[^1].Timestamp;
        }

        TrimRecent();
        RenumberRecent();
    }

    private void ResetLiveBuffer()
    {
        RecentSamples.Clear();
        _lastSampleTime = default;
        _lastSampleTagId = null;
        _seeded = false;
        AverageText = "--";
        SampleHint = "等待有效采集";
    }

    private void TrimRecent()
    {
        var keep = Math.Max(1, SampleCount);
        while (RecentSamples.Count > keep)
            RecentSamples.RemoveAt(0);
    }

    private void RenumberRecent()
    {
        var i = 1;
        foreach (var sample in RecentSamples)
            sample.Ordinal = i++;
    }

    private void Renumber()
    {
        var i = 1;
        foreach (var point in Points)
            point.Ordinal = i++;
    }

    private void UpdateFit()
    {
        if (!CalibrationEncoder.TryFitLine(ToModels(), out var slope, out var intercept))
        {
            SlopeText = "--";
            InterceptText = "--";
            FitHint = Points.Count < 2
                ? "至少 2 个标定点后显示拟合"
                : "实测值相同，无法拟合斜率";
            return;
        }

        SlopeText = FormatValue(slope);
        InterceptText = FormatValue(intercept);
        FitHint = "标准 = k × 实测 + b";
    }

    private static bool TryParseAddress(string? text, out ushort address)
    {
        text = text?.Trim() ?? string.Empty;
        return ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out address)
            || ushort.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out address);
    }

    private static bool TryParseNumber(string? text, out double value)
    {
        text = text?.Trim() ?? string.Empty;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static string FormatValue(double value)
    {
        var abs = Math.Abs(value);
        if (abs == 0) return "0";
        if (abs >= 1000) return value.ToString("0.##", CultureInfo.CurrentCulture);
        if (abs >= 1) return value.ToString("0.####", CultureInfo.CurrentCulture);
        return value.ToString("G6", CultureInfo.CurrentCulture);
    }
}
