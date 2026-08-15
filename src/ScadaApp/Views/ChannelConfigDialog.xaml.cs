using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScadaApp.Models;

namespace ScadaApp.Views;

public partial class ChannelConfigDialog : Window
{
    private readonly ChannelConfig _config;
    private readonly Snapshot _snapshot;

    public ChannelConfigDialog(ChannelConfig config, IEnumerable<string> ports, IEnumerable<int> baudRates)
    {
        _config = config;
        _snapshot = Snapshot.Capture(config);
        InitializeComponent();

        var portList = ports
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!string.IsNullOrWhiteSpace(config.PortName) &&
            !portList.Exists(p => string.Equals(p, config.PortName, StringComparison.OrdinalIgnoreCase)))
            portList.Insert(0, config.PortName.Trim());

        PortCombo.ItemsSource = portList;
        BaudCombo.ItemsSource = baudRates;
        DataContext = config;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        PortCombo.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
        PortCombo.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
        BaudCombo.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();

        var port = (PortCombo.SelectedItem as string)?.Trim();
        if (string.IsNullOrWhiteSpace(port))
            port = PortCombo.Text?.Trim();

        _config.Name = _config.Name?.Trim() ?? string.Empty;
        _config.PortName = port ?? string.Empty;
        if (_config.SlaveId is < 1 or > 247)
            _config.SlaveId = 1;

        foreach (var tag in _config.Tags)
            tag.SlaveId = _config.SlaveId;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _snapshot.Restore(_config);
        DialogResult = false;
        Close();
    }

    public static bool Edit(ChannelConfig config, IEnumerable<string> ports, IEnumerable<int> baudRates)
    {
        var dialog = new ChannelConfigDialog(config, ports, baudRates)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog() == true;
    }

    private sealed record Snapshot(
        string Name,
        string PortName,
        int BaudRate,
        int PollingIntervalMs,
        byte SlaveId)
    {
        public static Snapshot Capture(ChannelConfig config) => new(
            config.Name,
            config.PortName,
            config.BaudRate,
            config.PollingIntervalMs,
            config.SlaveId);

        public void Restore(ChannelConfig config)
        {
            config.Name = Name;
            config.PortName = PortName;
            config.BaudRate = BaudRate;
            config.PollingIntervalMs = PollingIntervalMs;
            config.SlaveId = SlaveId;
        }
    }
}
