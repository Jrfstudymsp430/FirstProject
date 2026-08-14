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
            object raw = tag.FunctionCode switch
            {
                ModbusFunctionCode.ReadCoils =>
                    (await Task.Run(() => _master!.ReadCoils(tag.SlaveId, tag.Address, 1), cancellationToken).ConfigureAwait(false))[0],
                ModbusFunctionCode.ReadDiscreteInputs =>
                    (await Task.Run(() => _master!.ReadInputs(tag.SlaveId, tag.Address, 1), cancellationToken).ConfigureAwait(false))[0],
                ModbusFunctionCode.ReadHoldingRegisters =>
                    ParseRegisters(await Task.Run(() => _master!.ReadHoldingRegisters(tag.SlaveId, tag.Address, tag.RegisterCount), cancellationToken).ConfigureAwait(false), tag.DataType),
                ModbusFunctionCode.ReadInputRegisters =>
                    ParseRegisters(await Task.Run(() => _master!.ReadInputRegisters(tag.SlaveId, tag.Address, tag.RegisterCount), cancellationToken).ConfigureAwait(false), tag.DataType),
                _ => throw new NotSupportedException($"不支持的功能码: {tag.FunctionCode}")
            };

            result.RawValue = raw;
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
                case ModbusFunctionCode.ReadCoils:
                case ModbusFunctionCode.WriteSingleCoil:
                    var coilValue = Convert.ToBoolean(value);
                    await Task.Run(() => _master!.WriteSingleCoil(tag.SlaveId, tag.Address, coilValue), cancellationToken).ConfigureAwait(false);
                    break;

                case ModbusFunctionCode.ReadHoldingRegisters:
                case ModbusFunctionCode.WriteSingleRegister:
                    if (tag.DataType == TagDataType.Bool)
                        throw new NotSupportedException("Bool 类型请使用线圈写入");

                    if (tag.RegisterCount == 1)
                    {
                        var reg = Convert.ToUInt16(value);
                        await Task.Run(() => _master!.WriteSingleRegister(tag.SlaveId, tag.Address, reg), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        var registers = EncodeRegisters(value, tag.DataType);
                        await Task.Run(() => _master!.WriteMultipleRegisters(tag.SlaveId, tag.Address, registers), cancellationToken).ConfigureAwait(false);
                    }
                    break;

                default:
                    throw new NotSupportedException($"写入不支持功能码: {tag.FunctionCode}");
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
            TagDataType.Bool => registers[0] != 0,
            TagDataType.Int16 => (short)registers[0],
            TagDataType.UInt16 => registers[0],
            TagDataType.Int32 => unchecked((int)(((uint)registers[0] << 16) | registers[1])),
            TagDataType.UInt32 => ((uint)registers[0] << 16) | registers[1],
            TagDataType.Float32 => BitConverter.ToSingle(BitConverter.GetBytes(((uint)registers[0] << 16) | registers[1]), 0),
            _ => registers[0]
        };
    }

    private static ushort[] EncodeRegisters(object value, TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Int32 or TagDataType.UInt32 =>
                SplitToRegisters(Convert.ToUInt32(value)),
            TagDataType.Float32 =>
                SplitToRegisters(BitConverter.ToUInt32(BitConverter.GetBytes(Convert.ToSingle(value)))),
            _ => new[] { Convert.ToUInt16(value) }
        };
    }

    private static ushort[] SplitToRegisters(uint value)
    {
        return new[] { (ushort)(value >> 16), (ushort)(value & 0xFFFF) };
    }

    private static string FormatValue(object raw, TagPoint tag)
    {
        if (raw is bool b)
            return b ? "ON" : "OFF";

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
