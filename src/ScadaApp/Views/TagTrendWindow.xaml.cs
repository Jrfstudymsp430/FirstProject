using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ScadaApp.Helpers;
using ScadaApp.ViewModels;

namespace ScadaApp.Views;

public partial class TagTrendWindow : Window
{
    private static readonly Dictionary<string, TagTrendWindow> OpenWindows = new();

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly string _tagId;
    private TagItemViewModel _tag;
    private Rect _restoreBounds;
    private bool _isMaximized;

    public TagTrendWindow(TagItemViewModel tag)
    {
        InitializeComponent();
        WindowWorkAreaHelper.Attach(this);
        _tag = tag;
        _tagId = tag.TagId;
        Chart.Buffer = tag.Trend;
        TitleText.Text = $"点位曲线 · {tag.Name}";
        Title = TitleText.Text;
        _timer.Tick += (_, _) => RefreshStats();
        Loaded += (_, _) =>
        {
            _restoreBounds = new Rect(Left, Top, Width, Height);
            _timer.Start();
            RefreshStats();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            OpenWindows.Remove(_tagId);
        };
    }

    public static void Show(TagItemViewModel tag, Window? owner)
    {
        if (OpenWindows.TryGetValue(tag.TagId, out var existing))
        {
            existing.Rebind(tag);
            existing.Activate();
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            return;
        }

        var window = new TagTrendWindow(tag);
        if (owner != null)
            window.Owner = owner;
        OpenWindows[tag.TagId] = window;
        window.Show();
    }

    public static void RebindAll(IEnumerable<TagItemViewModel> tags)
    {
        foreach (var tag in tags)
        {
            if (OpenWindows.TryGetValue(tag.TagId, out var window))
                window.Rebind(tag);
        }
    }

    public static void CloseIfOpen(string tagId)
    {
        if (!OpenWindows.TryGetValue(tagId, out var window))
            return;
        window.Close();
    }

    public void Rebind(TagItemViewModel tag)
    {
        _tag = tag;
        Chart.Buffer = tag.Trend;
        TitleText.Text = $"点位曲线 · {tag.Name}";
        Title = TitleText.Text;
        RefreshStats();
    }

    private void RefreshStats()
    {
        if (Chart.Paused)
            return;

        CurrentText.Text = _tag.DisplayValue;
        var window = Chart.WindowSeconds > 0
            ? TimeSpan.FromSeconds(Chart.WindowSeconds)
            : TimeSpan.Zero;

        if (!_tag.Trend.TryGetStats(window, out var min, out var max, out var avg, out _, out var count) || count == 0)
        {
            MinText.Text = "--";
            MaxText.Text = "--";
            AvgText.Text = "--";
            return;
        }

        MinText.Text = Format(min);
        MaxText.Text = Format(max);
        AvgText.Text = Format(avg);
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        Chart.Paused = !Chart.Paused;
        if (Chart.Paused)
        {
            PauseButton.Content = "继续刷新";
            PauseButton.Style = (Style)FindResource("DangerButton");
            _timer.Stop();
        }
        else
        {
            PauseButton.Content = "暂停刷新";
            PauseButton.Style = (Style)FindResource("SecondaryButton");
            _timer.Start();
            RefreshStats();
        }
    }

    private void RangeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        Chart.WindowSeconds = double.TryParse(button.Tag?.ToString(), out var seconds) ? seconds : 0;
        SetActiveRange(button);
        RefreshStats();
    }

    private void SetActiveRange(Button active)
    {
        foreach (var button in new[] { Range1Button, Range5Button, Range15Button, RangeAllButton })
            button.Style = (Style)FindResource("SecondaryButton");

        active.Style = (Style)FindResource("SuccessButton");
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

    private static string Format(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1000) return value.ToString("0.##");
        if (abs >= 1) return value.ToString("0.###");
        return value.ToString("G4");
    }
}
