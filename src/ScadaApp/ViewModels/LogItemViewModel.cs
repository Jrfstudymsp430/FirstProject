using CommunityToolkit.Mvvm.ComponentModel;

namespace ScadaApp.ViewModels;

public partial class LogItemViewModel : ObservableObject
{
    public DateTime Timestamp { get; init; }
    public string Level { get; init; } = "Info";
    public string ChannelName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Display => $"[{Timestamp:HH:mm:ss}] [{Level}] {ChannelName}: {Message}";
}
