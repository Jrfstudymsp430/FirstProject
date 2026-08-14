using System.IO.Ports;
using System.Windows;
using System.Windows.Input;
using ScadaApp.Models;

namespace ScadaApp.Views;

public partial class ChannelConfigDialog : Window
{
    private readonly ChannelConfig _config;
    private readonly Snapshot _snapshot;

    public ChannelConfigDialog(
        ChannelConfig config,
        IEnumerable<string> ports,
        IEnumerable<int> baudRates,
        IEnumerable<Parity> parities,
        IEnumerable<StopBits> stopBits,
        IEnumerable<int> dataBits)
    {
        _config = config;
        _snapshot = Snapshot.Capture(config);
        InitializeComponent();

        PortCombo.ItemsSource = ports;
        BaudCombo.ItemsSource = baudRates;
        ParityCombo.ItemsSource = parities;
        StopBitsCombo.ItemsSource = stopBits;
        DataBitsCombo.ItemsSource = dataBits;
        DataContext = config;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        _config.Name = _config.Name?.Trim() ?? string.Empty;
        _config.PortName = _config.PortName?.Trim() ?? string.Empty;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _snapshot.Restore(_config);
        DialogResult = false;
        Close();
    }

    public static bool Edit(
        ChannelConfig config,
        IEnumerable<string> ports,
        IEnumerable<int> baudRates,
        IEnumerable<Parity> parities,
        IEnumerable<StopBits> stopBits,
        IEnumerable<int> dataBits)
    {
        var dialog = new ChannelConfigDialog(config, ports, baudRates, parities, stopBits, dataBits)
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
        Parity Parity,
        StopBits StopBits,
        int DataBits)
    {
        public static Snapshot Capture(ChannelConfig config) => new(
            config.Name,
            config.PortName,
            config.BaudRate,
            config.PollingIntervalMs,
            config.Parity,
            config.StopBits,
            config.DataBits);

        public void Restore(ChannelConfig config)
        {
            config.Name = Name;
            config.PortName = PortName;
            config.BaudRate = BaudRate;
            config.PollingIntervalMs = PollingIntervalMs;
            config.Parity = Parity;
            config.StopBits = StopBits;
            config.DataBits = DataBits;
        }
    }
}
