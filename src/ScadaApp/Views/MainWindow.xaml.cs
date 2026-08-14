using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ScadaApp.Helpers;
using ScadaApp.ViewModels;

namespace ScadaApp.Views;

public partial class MainWindow : Window
{
    private Rect _restoreBounds;
    private bool _isMaximized;
    private bool _isShuttingDown;

    public MainWindow()
    {
        InitializeComponent();
        WindowWorkAreaHelper.Attach(this);
        _restoreBounds = new Rect(Left, Top, Width, Height);
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isShuttingDown)
            return;

        // 先取消本次关闭，等串口清理完成后再异步 Close。
        // 若 ShutdownAsync 同步完成，直接 Close() 仍处于 Closing 过程中会抛 InvalidOperationException。
        e.Cancel = true;
        _isShuttingDown = true;
        Hide();

        try
        {
            if (DataContext is MainViewModel vm)
                await vm.ShutdownAsync();
        }
        catch
        {
            // 清理失败也继续退出
        }

        await Dispatcher.InvokeAsync(Close, DispatcherPriority.Background);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        WindowState = WindowState.Minimized;
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
        UpdateMaximizeButtonIcon();
    }

    private void RestoreFromMaximize()
    {
        WindowState = WindowState.Normal;
        Left = _restoreBounds.Left;
        Top = _restoreBounds.Top;
        Width = _restoreBounds.Width;
        Height = _restoreBounds.Height;
        _isMaximized = false;
        UpdateMaximizeButtonIcon();
    }

    private void UpdateMaximizeButtonIcon()
    {
        MaximizeIcon.Data = _isMaximized
            ? System.Windows.Media.Geometry.Parse("M3,1 H10 V8 M1,3 H8 V10 H1 Z")
            : System.Windows.Media.Geometry.Parse("M1,1 H9 V9 H1 Z");
        MaximizeButton.ToolTip = _isMaximized ? "还原" : "最大化";
    }
}
