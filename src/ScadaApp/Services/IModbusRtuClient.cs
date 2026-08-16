using ScadaApp.Models;

namespace ScadaApp.Services;

public interface IModbusRtuClient : IDisposable
{
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<TagValue> ReadTagAsync(TagPoint tag, CancellationToken cancellationToken = default);
    Task WriteTagAsync(TagPoint tag, object value, CancellationToken cancellationToken = default);
    Task WriteRegistersAsync(byte slaveId, ushort startAddress, ushort[] registers, CancellationToken cancellationToken = default);
}
