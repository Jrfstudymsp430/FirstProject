using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScadaApp.Models;
using ScadaApp.Services;
using ScadaApp.Views;

namespace ScadaApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IChannelManager _channelManager;
    private readonly TrendStore _trends = new();
    private readonly SynchronizationContext? _uiContext;

    [ObservableProperty] private ChannelItemViewModel? _selectedChannel;
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private string _connectionStatusText = "离线";
    [ObservableProperty] private bool _isGlobalRunning;
    [ObservableProperty] private int _channelCount;
    [ObservableProperty] private int _runningChannelCount;
    [ObservableProperty] private int _tagCount;
    [ObservableProperty] private int _goodTagCount;
    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

    public ObservableCollection<ChannelItemViewModel> Channels { get; } = new();
    public ObservableCollection<TagItemViewModel> MonitorTags { get; } = new();
    public ObservableCollection<TagItemViewModel> Tags { get; } = new();
    public ObservableCollection<LogItemViewModel> Logs { get; } = new();
    public ObservableCollection<string> AvailablePorts { get; } = new();
    private readonly List<TagItemViewModel> _selectedTags = new();

    public int[] BaudRates => SerialPortHelper.CommonBaudRates;
    public Array FunctionCodes => Enum.GetValues(typeof(ModbusFunctionCode));
    public Array DataTypes => Enum.GetValues(typeof(TagDataType));

    public MainViewModel() : this(new ChannelManager()) { }

    public MainViewModel(IChannelManager channelManager)
    {
        _channelManager = channelManager;
        _uiContext = SynchronizationContext.Current;

        _channelManager.LogAdded += (_, log) => RunOnUi(() => AddLog(log));
        _channelManager.TagValueUpdated += (_, value) => RunOnUi(() => UpdateTagValue(value));
        _channelManager.ChannelStateChanged += (_, _) => RunOnUi(RefreshAllChannelStates);

        LoadFromStorage();
        RefreshPorts();

        _clock.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _clock.Start();
    }

    partial void OnSelectedChannelChanged(ChannelItemViewModel? value)
    {
        LoadConfigTags(value);
        RefreshSummary();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var port in SerialPortHelper.GetAvailablePorts().OrderBy(p => p))
            AvailablePorts.Add(port);

        if (AvailablePorts.Count == 0)
            AvailablePorts.Add("COM1");
    }

    [RelayCommand]
    private void AddChannel()
    {
        var port = AvailablePorts.FirstOrDefault() ?? "COM1";
        var config = new ChannelConfig
        {
            Name = $"通道{Channels.Count + 1}",
            PortName = port
        };
        _channelManager.AddChannel(config);
        ConfigStorage.Save(_channelManager.Channels);
        LoadChannels();
        SelectedChannel = Channels.LastOrDefault();
        StatusText = $"已添加通道: {config.Name}";
    }

    [RelayCommand]
    private async Task RemoveChannelAsync()
    {
        if (SelectedChannel == null) return;

        if (!MessageDialog.Confirm("删除通道", $"确定删除通道「{SelectedChannel.Name}」？运行中的连接将停止。"))
            return;

        var name = SelectedChannel.Name;
        foreach (var tag in SelectedChannel.Config.Tags)
        {
            _trends.Remove(tag.Id);
            TagTrendWindow.CloseIfOpen(tag.Id);
        }
        await _channelManager.RemoveChannelAsync(SelectedChannel.Id);
        ConfigStorage.Save(_channelManager.Channels);
        LoadChannels();
        SelectedChannel = Channels.LastOrDefault();
        StatusText = $"已删除通道: {name}";
    }

    [RelayCommand]
    private async Task StartChannelAsync()
    {
        if (SelectedChannel == null) return;

        if (_channelManager.GetRunningChannel(SelectedChannel.Id) != null)
        {
            SelectedChannel.RefreshState();
            ApplyConnectedStatusText();
            return;
        }

        try
        {
            StatusText = $"正在启动 {SelectedChannel.Name}...";
            await _channelManager.StartChannelAsync(SelectedChannel.Id);
            SelectedChannel.RefreshState();
            RefreshSummary();
            ApplyConnectedStatusText();
        }
        catch (Exception ex)
        {
            StatusText = $"启动失败: {ex.Message}";
            MessageDialog.Alert("启动通道失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task StopChannelAsync()
    {
        if (SelectedChannel == null) return;

        await _channelManager.StopChannelAsync(SelectedChannel.Id);
        SelectedChannel.RefreshState();
        RefreshSummary();
        ApplyConnectedStatusText();
        if (Channels.All(c => c.State != ChannelState.Connected))
            StatusText = $"{SelectedChannel.Name} 已停止";
    }

    [RelayCommand]
    private async Task StartAllAsync()
    {
        StatusText = "正在启动所有通道...";
        await _channelManager.StartAllAsync();
        foreach (var ch in Channels) ch.RefreshState();
        IsGlobalRunning = true;
        RefreshSummary();
        ApplyConnectedStatusText();
    }

    [RelayCommand]
    private async Task StopAllAsync()
    {
        await _channelManager.StopAllAsync();
        foreach (var ch in Channels) ch.RefreshState();
        IsGlobalRunning = false;
        RefreshSummary();
        ApplyConnectedStatusText();
        StatusText = "所有通道已停止";
    }

    [RelayCommand]
    private void SaveConfig()
    {
        ConfigStorage.Save(_channelManager.Channels);
        StatusText = "配置已保存";
    }

    [RelayCommand]
    private void EditChannel()
    {
        if (SelectedChannel == null)
        {
            MessageDialog.Alert("通道参数", "请先选择一个通道。");
            return;
        }

        RefreshPorts();
        if (!ChannelConfigDialog.Edit(SelectedChannel.Config, AvailablePorts, BaudRates))
            return;

        _channelManager.UpdateChannel(SelectedChannel.Config);
        ConfigStorage.Save(_channelManager.Channels);
        SelectedChannel.RefreshState();
        ReloadAllTags();
        StatusText = $"已更新通道参数: {SelectedChannel.Name}";
    }

    [RelayCommand]
    private void AddTag()
    {
        if (SelectedChannel == null) return;

        var tag = new TagPoint
        {
            Name = $"Tag{SelectedChannel.Config.Tags.Count + 1}",
            Address = (ushort)(SelectedChannel.Config.Tags.Count * 2),
            SlaveId = SelectedChannel.Config.SlaveId,
            FunctionCode = ModbusFunctionCode.ReadHoldingRegisters,
            DataType = TagDataType.Float32
        };

        if (!TagConfigDialog.Edit(tag))
            return;

        SelectedChannel.Config.Tags.Add(tag);
        _channelManager.UpdateChannel(SelectedChannel.Config);
        ConfigStorage.Save(_channelManager.Channels);
        ReloadAllTags();
        StatusText = $"已添加标签: {tag.Name}";
    }

    [RelayCommand]
    private void EditTag(TagItemViewModel? tagVm)
    {
        if (SelectedChannel == null || tagVm == null) return;

        var tag = SelectedChannel.Config.Tags.FirstOrDefault(t => t.Id == tagVm.TagId);
        if (tag == null) return;

        if (!TagConfigDialog.Edit(tag))
            return;

        _channelManager.UpdateChannel(SelectedChannel.Config);
        ConfigStorage.Save(_channelManager.Channels);
        ReloadAllTags();
        StatusText = $"已更新标签: {tag.Name}";
    }

    [RelayCommand]
    private void RemoveTag(TagItemViewModel? tag)
    {
        if (SelectedChannel == null || tag == null) return;

        if (!MessageDialog.Confirm("删除标签", $"确定删除标签「{tag.Name}」？"))
            return;

        SelectedChannel.Config.Tags.RemoveAll(t => t.Id == tag.TagId);
        _trends.Remove(tag.TagId);
        TagTrendWindow.CloseIfOpen(tag.TagId);
        _channelManager.UpdateChannel(SelectedChannel.Config);
        ConfigStorage.Save(_channelManager.Channels);
        ReloadAllTags();
        StatusText = $"已删除标签: {tag.Name}";
    }

    [RelayCommand]
    private async Task WriteTagAsync(TagItemViewModel? tag)
    {
        if (tag == null || !tag.IsWritable) return;

        var defaultValue = tag.DisplayValue is "ERR" or "--" ? "0" : StripDisplayUnit(tag.DisplayValue);
        var input = InputDialog.Show(
            "写入数据",
            tag.Name,
            tag.DataType,
            defaultValue);

        if (string.IsNullOrWhiteSpace(input)) return;

        try
        {
            await tag.WriteAsync(input.Trim());
            StatusText = $"已写入 {tag.Name} = {input}";
        }
        catch (Exception ex)
        {
            MessageDialog.Alert("写入失败", ex.Message);
        }
    }

    public void SyncSelectedTags(System.Collections.IList selected)
    {
        _selectedTags.Clear();
        foreach (var item in selected)
        {
            if (item is TagItemViewModel tag)
                _selectedTags.Add(tag);
        }
    }

    [RelayCommand]
    private async Task WriteTagsBatchAsync()
    {
        if (SelectedChannel == null)
        {
            MessageDialog.Alert("批量写入", "请先选择一个通道。");
            return;
        }

        List<TagItemViewModel> targets;
        try
        {
            targets = ResolveBatchWriteTags();
            TagBlockWriter.EnsureWritableConsecutive(targets.Select(t => t.Point).ToList());
        }
        catch (Exception ex)
        {
            MessageDialog.Alert("批量写入", ex.Message);
            return;
        }

        var rows = BatchWriteDialog.Show(targets);
        if (rows == null || rows.Count == 0)
            return;

        try
        {
            var parsed = new List<(string TagId, object Value)>(rows.Count);
            foreach (var row in rows)
            {
                var tag = targets.First(t => t.TagId == row.TagId);
                parsed.Add((row.TagId, TagBlockWriter.ParseValue(tag.Point, row.Input)));
            }

            await _channelManager.WriteTagsAsync(SelectedChannel.Id, parsed);
            StatusText = $"已连续写入 {parsed.Count} 个点";
        }
        catch (FormatException)
        {
            MessageDialog.Alert("批量写入", "写入值格式不正确。");
        }
        catch (Exception ex)
        {
            MessageDialog.Alert("写入失败", ex.Message);
        }
    }

    private List<TagItemViewModel> ResolveBatchWriteTags()
    {
        if (_selectedTags.Count >= 2)
            return _selectedTags.ToList();

        if (_selectedTags.Count == 1)
            throw new InvalidOperationException("请按住 Ctrl 或 Shift 再选中相邻地址的可写点。");

        var writable = Tags.Where(t => t.IsWritable).OrderBy(t => t.Address).ToList();
        if (writable.Count < 2)
            throw new InvalidOperationException("当前通道可写点不足 2 个。请把连续点的功能码设为 06 或 16，并在点表中连选。");

        try
        {
            TagBlockWriter.EnsureWritableConsecutive(writable.Select(t => t.Point).ToList());
            return writable;
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException("可写点地址不连续。请在点表中按住 Shift 选中一段首尾相接的点。");
        }
    }

    [RelayCommand]
    private void ShowTagTrend(TagItemViewModel? tag)
    {
        if (tag == null || !tag.TrendEnabled)
            return;
        TagTrendWindow.Show(tag, Application.Current.MainWindow);
        StatusText = $"已打开曲线: {tag.Name}";
    }

    private void LoadFromStorage()
    {
        foreach (var existing in _channelManager.Channels.ToList())
            _channelManager.RemoveChannel(existing.Id);

        foreach (var config in ConfigStorage.Load())
            _channelManager.AddChannel(config);

        LoadChannels();
        SelectedChannel = Channels.FirstOrDefault();
    }

    private void LoadChannels()
    {
        var selectedId = SelectedChannel?.Id;
        Channels.Clear();
        foreach (var config in _channelManager.Channels)
        {
            var vm = new ChannelItemViewModel(_channelManager, config);
            vm.RefreshState();
            Channels.Add(vm);
        }

        RebuildMonitorTags();
        SelectedChannel = Channels.FirstOrDefault(c => c.Id == selectedId) ?? Channels.FirstOrDefault();
        LoadConfigTags(SelectedChannel);
        SyncTrendWindows();
        RefreshSummary();
        ApplyConnectedStatusText();
    }

    private void ReloadAllTags()
    {
        RebuildMonitorTags();
        LoadConfigTags(SelectedChannel);
        SyncTrendWindows();
        RefreshSummary();
    }

    private void SyncTrendWindows()
    {
        foreach (var tag in MonitorTags.Where(t => !t.TrendEnabled))
            TagTrendWindow.CloseIfOpen(tag.TagId);
        TagTrendWindow.RebindAll(MonitorTags.Where(t => t.TrendEnabled));
    }

    private void RebuildMonitorTags()
    {
        MonitorTags.Clear();
        foreach (var channel in Channels)
            channel.MonitorTags.Clear();

        foreach (var channel in Channels)
        {
            foreach (var tag in channel.Config.Tags)
            {
                var vm = new TagItemViewModel(_channelManager, channel.Config, tag, _trends.GetOrCreate(tag.Id));
                var running = _channelManager.GetRunningChannel(channel.Id);
                if (running?.TagValues.TryGetValue(tag.Id, out var value) == true && value != null)
                    vm.Update(value, recordTrend: false);
                MonitorTags.Add(vm);
                channel.MonitorTags.Add(vm);
            }

            channel.RefreshState();
        }
    }

    private void LoadConfigTags(ChannelItemViewModel? channel)
    {
        Tags.Clear();
        if (channel == null)
            return;

        var n = 1;
        foreach (var tag in MonitorTags.Where(t => t.ChannelId == channel.Id))
        {
            tag.Ordinal = n++;
            Tags.Add(tag);
        }
    }

    private void UpdateTagValue(TagValue value)
    {
        var tag = MonitorTags.FirstOrDefault(t => t.TagId == value.TagId);
        if (tag != null)
        {
            tag.Update(value);
        }
        else if (value.Quality == "Good" && value.NumericValue is double number)
        {
            var point = Channels.SelectMany(c => c.Config.Tags).FirstOrDefault(t => t.Id == value.TagId);
            if (point is { TrendEnabled: true })
            {
                _trends.GetOrCreate(value.TagId).Add(
                    value.Timestamp == default ? DateTime.Now : value.Timestamp,
                    number);
            }
        }

        var channel = Channels.FirstOrDefault(c =>
            c.Config.Tags.Any(t => t.Id == value.TagId));
        channel?.RefreshState();
        RefreshSummary();
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
        ApplyConnectedStatusText(clearedLogs: true);
    }

    private void RefreshSummary()
    {
        ChannelCount = Channels.Count;
        RunningChannelCount = Channels.Count(c => c.IsRunning);
        TagCount = MonitorTags.Count;
        GoodTagCount = MonitorTags.Count(t => t.Quality == "Good");
    }

    private void RefreshAllChannelStates()
    {
        foreach (var ch in Channels)
            ch.RefreshState();
        RefreshSummary();
        ApplyConnectedStatusText();
    }

    /// <summary>
    /// 连接成功后状态栏与日志标题保持显示「已连接」，直到用户主动停止。
    /// </summary>
    private void ApplyConnectedStatusText(bool clearedLogs = false)
    {
        var connected = Channels.Where(c => c.State == ChannelState.Connected).ToList();
        if (connected.Count > 0)
        {
            ConnectionStatusText = connected.Count == 1
                ? $"{connected[0].Name} 已连接"
                : $"已连接 {connected.Count} 个通道";
            StatusText = ConnectionStatusText;
            return;
        }

        if (Channels.Any(c => c.State == ChannelState.Connecting))
        {
            ConnectionStatusText = "连接中";
            return;
        }

        ConnectionStatusText = "离线";
        if (clearedLogs)
            StatusText = "日志已清空";
    }

    private void AddLog(LogEntry log)
    {
        Logs.Insert(0, new LogItemViewModel
        {
            Timestamp = log.Timestamp,
            Level = log.Level,
            ChannelName = log.ChannelName,
            Message = log.Message
        });

        while (Logs.Count > 500)
            Logs.RemoveAt(Logs.Count - 1);
    }

    private void RunOnUi(Action action)
    {
        if (_uiContext != null)
            _uiContext.Post(_ => action(), null);
        else
            action();
    }

    public async Task ShutdownAsync()
    {
        _clock.Stop();
        await _channelManager.StopAllAsync();
        ConfigStorage.Save(_channelManager.Channels);
    }

    private static string StripDisplayUnit(string display)
    {
        var space = display.LastIndexOf(' ');
        if (space > 0 && double.TryParse(display[..space], out _))
            return display[..space];
        return display;
    }
}
