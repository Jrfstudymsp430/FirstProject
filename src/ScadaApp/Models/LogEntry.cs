namespace ScadaApp.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Level { get; set; } = "Info";
    public string ChannelName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
