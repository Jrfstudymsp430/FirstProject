using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScadaApp.Models;
using ScadaApp.Services;
using ScadaApp.Views;

namespace ScadaApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IChannelManager _channelManager;
    private readonly SynchronizationContext? _uiContext;

    [ObservableProperty] private ChannelItemViewModel? _selectedChannel;
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private bool _isGlobalRunning;
    [ObservableProperty] private int _channelCount;
    [ObservableProperty] private int _runningChannelCount;
    [ObservableProperty] private int _tagCount;
    [ObservableProperty] private int _goodTagCount;

    public ObservableCollection<ChannelItemViewModel> Channels { get; } = new();
    public ObservableCollection<TagItemViewModel> Tags { get; } = new();
    public ObservableCollection<LogItemViewModel> Logs { get; } = new();
    public ObservableCollection<string> AvailablePorts { get; } = new();

    public int[] BaudRates => SerialPortHelper.CommonBaudRates;
    public int[] DataBitsOptions => SerialPortHelper.DataBitsOptions;
    public Array ParityOptions => SerialPortHelper.ParityOptions;
    public Array StopBitsOptions => SerialPortHelper.StopBitsOptions;
    public Array FunctionCodes => Enum.GetValues(typeof(ModbusFunctionCode));
    public Array DataTypes => Enum.GetValues(typeof(TagDataType));

    public MainViewModel() : this(new ChannelManager()) { }

    public MainViewModel(IChannelManager channelManager)
    {
        _channelManager = channelManager;
        _uiContext = SynchronizationContext.Current;

        _channelManager.LogAdded += (_, log) => RunOnUi(() => AddLog(log));
        _channelManager.TagValueUpdated += (_, value) => RunOnUi(() => UpdateTagValue(value));

        LoadFromStorage();
        RefreshPorts();
    }

    partial void OnSelectedChannelChanged(ChannelItemViewModel? value)
    {
        LoadTagsForChannel(value);
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
        await _channelManager.RemoveChannelAsync(SelectedChannel.Id);
        ConfigStorage.Save(_channelManager.Channels);
        LoadChannels();
        SelectedChannel = Channels.FirstOrDefault();
        StatusText = $"已删除通道: {name}";
    }

    [RelayCommand]
    private async Task StartChannelAsync()
    {
        if (SelectedChannel == null) return;

        if (_channelManager.GetRunningChannel(SelectedChannel.Id) != null)
        {
            StatusText = $"{SelectedChannel.Name} 已在运行";
            return;
        }

        try
        {
            StatusText = $"正在启动 {SelectedChannel.Name}...";
            await _channelManager.StartChannelAsync(SelectedChannel.Id);
            SelectedChannel.RefreshState();
            RefreshSummary();
            StatusText = $"{SelectedChannel.Name} 已启动";
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
        StatusText = "所有通道已启动";
    }

    [RelayCommand]
    private async Task StopAllAsync()
    {
        await _channelManager.StopAllAsync();
        foreach (var ch in Channels) ch.RefreshState();
        IsGlobalRunning = false;
        RefreshSummary();
        StatusText = "所有通道已停止";
    }

    [RelayCommand]
    private void SaveConfig()
    {
        ConfigStorage.Save(_channelManager.Channels);
        StatusText = "配置已保存";
    }

    [RelayCommand]
    private void AddTag()
    {
        if (SelectedChannel == null) return;

        var tag = new TagPoint
        {
            Name = $"Tag{SelectedChannel.Config.Tags.Count + 1}",
            Address = (ushort)(SelectedChannel.Config.Tags.Count * 2)
        };

        if (!TagConfigDialog.Edit(tag))
            return;

        SelectedChannel.Config.Tags.Add(tag);
        _channelManager.UpdateChannel(SelectedChannel.Config);
        ConfigStorage.Save(_channelManager.Channels);
        LoadTagsForChannel(SelectedChannel);
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
        LoadTagsForChannel(SelectedChannel);
        StatusText = $"已更新标签: {tag.Name}";
    }

    [RelayCommand]
    private void RemoveTag(TagItemViewModel? tag)
    {
        if (SelectedChannel == null || tag == null) return;

        if (!MessageDialog.Confirm("删除标签", $"确定删除标签「{tag.Name}」？"))
            return;

        SelectedChannel.Config.Tags.RemoveAll(t => t.Id == tag.TagId);
        _channelManager.UpdateChannel(SelectedChannel.Config);
        ConfigStorage.Save(_channelManager.Channels);
        LoadTagsForChannel(SelectedChannel);
        StatusText = $"已删除标签: {tag.Name}";
    }

    [RelayCommand]
    private async Task WriteTagAsync(TagItemViewModel? tag)
    {
        if (tag == null || !tag.IsWritable) return;

        var defaultValue = tag.DisplayValue is "ERR" or "--" ? "0" : StripDisplayUnit(tag.DisplayValue);
        var input = InputDialog.Show(
            "写入 Modbus 点",
            $"写入 {tag.Name} ({tag.DataType})",
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

        SelectedChannel = Channels.FirstOrDefault(c => c.Id == selectedId) ?? Channels.FirstOrDefault();
        RefreshSummary();
    }

    private void LoadTagsForChannel(ChannelItemViewModel? channel)
    {
        Tags.Clear();
        if (channel == null)
        {
            RefreshSummary();
            return;
        }

        foreach (var tag in channel.Config.Tags)
        {
            var vm = new TagItemViewModel(_channelManager, channel.Config, tag);
            var running = _channelManager.GetRunningChannel(channel.Id);
            if (running?.TagValues.TryGetValue(tag.Id, out var value) == true && value != null)
                vm.Update(value);
            Tags.Add(vm);
        }
        RefreshSummary();
    }

    private void UpdateTagValue(TagValue value)
    {
        var tag = Tags.FirstOrDefault(t => t.TagId == value.TagId);
        tag?.Update(value);

        var channel = Channels.FirstOrDefault(c =>
            c.Config.Tags.Any(t => t.Id == value.TagId));
        channel?.RefreshState();
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        ChannelCount = Channels.Count;
        RunningChannelCount = Channels.Count(c => c.IsRunning);
        TagCount = Tags.Count;
        GoodTagCount = Tags.Count(t => t.Quality == "Good");
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
