using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ScadaApp.Helpers;
using ScadaApp.Services;
using ScadaApp.ViewModels;

namespace ScadaApp.Views;

public partial class CalibrationWindow : Window
{
    private static readonly Dictionary<string, CalibrationWindow> OpenWindows = new();

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly CalibrationViewModel _vm;
    private Rect _restoreBounds;
    private bool _isMaximized;

    public CalibrationWindow(ChannelItemViewModel channel, IChannelManager channelManager)
    {
        InitializeComponent();
        WindowWorkAreaHelper.Attach(this);
        _vm = new CalibrationViewModel(channelManager, channel.Config);
        DataContext = _vm;
        _vm.BindTags(channel.MonitorTags, channel.Config);

        _timer.Tick += (_, _) => _vm.RefreshLive();
        Loaded += (_, _) =>
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);
            _timer.Start();
            _vm.RefreshLive();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _vm.Persist();
            OpenWindows.Remove(_vm.ChannelId);
        };
    }

    public static void Show(ChannelItemViewModel channel, IChannelManager channelManager, Window? owner)
    {
        if (OpenWindows.TryGetValue(channel.Id, out var existing))
        {
            existing.Rebind(channel);
            existing.Activate();
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            return;
        }

        var window = new CalibrationWindow(channel, channelManager);
        if (owner != null)
            window.Owner = owner;
        OpenWindows[channel.Id] = window;
        window.Show();
    }

    public static void RebindAll(IEnumerable<ChannelItemViewModel> channels)
    {
        foreach (var channel in channels)
        {
            if (OpenWindows.TryGetValue(channel.Id, out var window))
                window.Rebind(channel);
        }
    }

    public static void CloseIfOpen(string channelId)
    {
        if (!OpenWindows.TryGetValue(channelId, out var window))
            return;
        window.Close();
    }

    public static void CloseAll()
    {
        foreach (var window in OpenWindows.Values.ToList())
            window.Close();
    }

    public void Rebind(ChannelItemViewModel channel)
    {
        _vm.BindTags(channel.MonitorTags, channel.Config);
        Title = _vm.Title;
    }

    private void StandardInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        if (_vm.AddPointCommand.CanExecute(null))
            _vm.AddPointCommand.Execute(null);
        e.Handled = true;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            e.Handled = true;
            return;
        }

        if (_isMaximized)
            RestoreFromMaximize();

        DragMove();
        e.Handled = true;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Close();
    }

    private void ToggleMaximize()
    {
        if (_isMaximized)
            RestoreFromMaximize();
        else
            MaximizeToWorkArea();
    }

    private void MaximizeToWorkArea()
    {
        _restoreBounds = new Rect(Left, Top, Width, Height);
        var area = SystemParameters.WorkArea;
        WindowState = WindowState.Normal;
        Left = area.Left;
        Top = area.Top;
        Width = area.Width;
        Height = area.Height;
        _isMaximized = true;
        MaximizeIcon.Data = Geometry.Parse("M3,1 H10 V8 M1,3 H8 V10 H1 Z");
    }

    private void RestoreFromMaximize()
    {
        WindowState = WindowState.Normal;
        Left = _restoreBounds.Left;
        Top = _restoreBounds.Top;
        Width = _restoreBounds.Width;
        Height = _restoreBounds.Height;
        _isMaximized = false;
        MaximizeIcon.Data = Geometry.Parse("M1,1 H9 V9 H1 Z");
    }
}
