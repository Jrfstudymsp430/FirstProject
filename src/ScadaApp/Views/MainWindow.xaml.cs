using System.Windows;
using System.Windows.Input;
using ScadaApp.Helpers;

namespace ScadaApp.Views;

public partial class MainWindow : Window
{
    private Rect _restoreBounds;
    private bool _isMaximized;

    public MainWindow()
    {
        InitializeComponent();
        WindowWorkAreaHelper.Attach(this);
        _restoreBounds = new Rect(Left, Top, Width, Height);
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
            ? System.Windows.Media.Geometry.Parse("M2,2 H10 V10 H2 Z M0,0 H8 V1 H0 Z")
            : System.Windows.Media.Geometry.Parse("M0,0 H9 V9 H0 Z");
        MaximizeButton.ToolTip = _isMaximized ? "还原" : "最大化";
    }
}
