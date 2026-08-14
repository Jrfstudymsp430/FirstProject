namespace ScadaApp.Models;

/// <summary>
/// 串口通道配置（一个通道对应一个 COM 口）
/// </summary>
public class ChannelConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "通道1";
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public System.IO.Ports.Parity Parity { get; set; } = System.IO.Ports.Parity.None;
    public System.IO.Ports.StopBits StopBits { get; set; } = System.IO.Ports.StopBits.One;
    public int ReadTimeout { get; set; } = 1000;
    public int WriteTimeout { get; set; } = 1000;
    public int PollingIntervalMs { get; set; } = 500;
    public bool IsEnabled { get; set; } = true;
    public List<TagPoint> Tags { get; set; } = new();
}
