using System.IO.Ports;
using NModbus;
using NModbus.Serial;
using ScadaApp.Models;

namespace ScadaApp.Services;

/// <summary>
/// 单通道 Modbus RTU 客户端，基于 NModbus 实现标准协议
/// </summary>
public sealed class ModbusRtuClient : IModbusRtuClient
{
    private readonly ChannelConfig _config;
    private SerialPort? _serialPort;
    private IModbusMaster? _master;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ModbusRtuClient(ChannelConfig config)
    {
        _config = config;
    }

    public bool IsConnected => _serialPort?.IsOpen == true;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
                return;

            _serialPort = new SerialPort(_config.PortName)
            {
                BaudRate = _config.BaudRate,
                DataBits = _config.DataBits,
                Parity = _config.Parity,
                StopBits = _config.StopBits,
                ReadTimeout = _config.ReadTimeout,
                WriteTimeout = _config.WriteTimeout
            };

            await Task.Run(() => _serialPort.Open(), cancellationToken).ConfigureAwait(false);

            var factory = new ModbusFactory();
            _master = factory.CreateRtuMaster(new SerialPortAdapter(_serialPort));
            _master.Transport.Retries = 3;
            _master.Transport.WaitToRetryMilliseconds = 250;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _master?.Dispose();
            _master = null;

            if (_serialPort?.IsOpen == true)
            {
                await Task.Run(() => _serialPort.Close()).ConfigureAwait(false);
            }

            _serialPort?.Dispose();
            _serialPort = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TagValue> ReadTagAsync(TagPoint tag, CancellationToken cancellationToken = default)
    {
        var result = new TagValue
        {
            TagId = tag.Id,
            Timestamp = DateTime.Now
        };

        if (_master == null || !IsConnected)
        {
            result.Quality = "Bad";
            result.ErrorMessage = "通道未连接";
            return result;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            object raw;
            // 采集统一使用 03 读保持寄存器
            var registers = await Task.Run(
                () => _master!.ReadHoldingRegisters(tag.SlaveId, tag.Address, tag.RegisterCount),
                cancellationToken).ConfigureAwait(false);
            raw = ParseRegisters(registers, tag.DataType);

            result.RawValue = raw;
            result.NumericValue = Convert.ToDouble(raw) * tag.Scale + tag.Offset;
            result.DisplayValue = FormatValue(raw, tag);
            result.Quality = "Good";
        }
        catch (Exception ex)
        {
            result.Quality = "Bad";
            result.ErrorMessage = ex.Message;
            result.DisplayValue = "ERR";
        }
        finally
        {
            _lock.Release();
        }

        return result;
    }

    public async Task WriteTagAsync(TagPoint tag, object value, CancellationToken cancellationToken = default)
    {
        if (!tag.IsWritable)
            throw new InvalidOperationException($"标签 {tag.Name} 不可写");

        if (_master == null || !IsConnected)
            throw new InvalidOperationException("通道未连接");

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            switch (tag.FunctionCode)
            {
                case ModbusFunctionCode.WriteSingleRegister:
                    var reg = Convert.ToUInt16(value);
                    await Task.Run(() => _master!.WriteSingleRegister(tag.SlaveId, tag.Address, reg), cancellationToken).ConfigureAwait(false);
                    break;

                case ModbusFunctionCode.WriteMultipleRegisters:
                    var registers = EncodeRegisters(value, tag.DataType);
                    await Task.Run(() => _master!.WriteMultipleRegisters(tag.SlaveId, tag.Address, registers), cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new NotSupportedException("当前点为只读（功能码 03），不可写入");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static object ParseRegisters(ushort[] registers, TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.UInt16 => registers[0],
            TagDataType.Float32 => RegistersToFloatCdab(registers),
            _ => registers[0]
        };
    }

    private static ushort[] EncodeRegisters(object value, TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Float32 => FloatToRegistersCdab(Convert.ToSingle(value)),
            _ => new[] { Convert.ToUInt16(value) }
        };
    }

    /// <summary>
    /// CDAB：第一个寄存器为 CD（低字），第二个为 AB（高字）。
    /// </summary>
    private static float RegistersToFloatCdab(ushort[] registers)
    {
        var bytes = new[]
        {
            (byte)(registers[1] >> 8),
            (byte)(registers[1] & 0xFF),
            (byte)(registers[0] >> 8),
            (byte)(registers[0] & 0xFF)
        };
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    private static ushort[] FloatToRegistersCdab(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);

        var cd = (ushort)((bytes[2] << 8) | bytes[3]);
        var ab = (ushort)((bytes[0] << 8) | bytes[1]);
        return new[] { cd, ab };
    }

    private static string FormatValue(object raw, TagPoint tag)
    {
        var scaled = Convert.ToDouble(raw) * tag.Scale + tag.Offset;
        if (string.IsNullOrEmpty(tag.Unit))
        {
            if (Math.Abs(tag.Scale - 1.0) < 1e-9 && Math.Abs(tag.Offset) < 1e-9)
                return raw is IFormattable f ? f.ToString("G", null) ?? scaled.ToString("G") : scaled.ToString("G");
            return scaled.ToString("G");
        }

        return $"{scaled:F2} {tag.Unit}";
    }

    public void Dispose()
    {
        _master?.Dispose();
        _serialPort?.Dispose();
        _lock.Dispose();
    }
}
