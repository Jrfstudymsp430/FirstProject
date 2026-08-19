using System.Globalization;
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
    private long _txCount;
    private long _rxCount;

    public ModbusRtuClient(ChannelConfig config)
    {
        _config = config;
    }

    public bool IsConnected => _serialPort?.IsOpen == true;
    public long TxCount => Interlocked.Read(ref _txCount);
    public long RxCount => Interlocked.Read(ref _rxCount);

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
        var values = await ReadTagsAsync(new[] { tag }, cancellationToken).ConfigureAwait(false);
        return values[0];
    }

    public async Task<IReadOnlyList<TagValue>> ReadTagsAsync(
        IReadOnlyList<TagPoint> tags,
        CancellationToken cancellationToken = default)
    {
        if (tags.Count == 0)
            return Array.Empty<TagValue>();

        if (_master == null || !IsConnected)
        {
            return tags.Select(t => FailedValue(t, "通道未连接")).ToList();
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var results = new List<TagValue>(tags.Count);
            foreach (var block in TagBlockReader.Split(tags))
            {
                Interlocked.Increment(ref _txCount);
                ushort[] registers;
                try
                {
                    var count = block.Count;
                    var start = block.Start;
                    registers = await Task.Run(
                        () => _master!.ReadHoldingRegisters(block.SlaveId, start, count),
                        cancellationToken).ConfigureAwait(false);
                    Interlocked.Increment(ref _rxCount);
                }
                catch (Exception ex)
                {
                    foreach (var tag in block.Tags)
                        results.Add(FailedValue(tag, ex.Message));
                    continue;
                }

                foreach (var tag in block.Tags)
                    results.Add(DecodeFromBlock(tag, registers, block.Start));
            }

            return results;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static TagValue FailedValue(TagPoint tag, string error) => new()
    {
        TagId = tag.Id,
        Timestamp = DateTime.Now,
        Quality = "Bad",
        DisplayValue = "ERR",
        ErrorMessage = error
    };

    private static TagValue DecodeFromBlock(TagPoint tag, ushort[] registers, ushort blockStart)
    {
        var result = new TagValue
        {
            TagId = tag.Id,
            Timestamp = DateTime.Now
        };

        var offset = tag.Address - blockStart;
        if (offset < 0 || offset + tag.RegisterCount > registers.Length)
        {
            result.Quality = "Bad";
            result.ErrorMessage = "块读取范围不足";
            result.DisplayValue = "ERR";
            return result;
        }

        var slice = new ushort[tag.RegisterCount];
        Array.Copy(registers, offset, slice, 0, tag.RegisterCount);
        var raw = ParseRegisters(slice, tag.DataType);
        result.RawValue = raw;
        result.NumericValue = Convert.ToDouble(raw) * tag.Scale + tag.Offset;
        result.DisplayValue = FormatValue(raw, tag);
        result.Quality = "Good";
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
                    await TrackAsync(
                        () => Task.Run(() => _master!.WriteSingleRegister(tag.SlaveId, tag.Address, reg), cancellationToken)).ConfigureAwait(false);
                    break;

                case ModbusFunctionCode.WriteMultipleRegisters:
                    var registers = EncodeRegisters(value, tag.DataType);
                    await TrackAsync(
                        () => Task.Run(() => _master!.WriteMultipleRegisters(tag.SlaveId, tag.Address, registers), cancellationToken)).ConfigureAwait(false);
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

    public async Task WriteRegistersAsync(
        byte slaveId,
        ushort startAddress,
        ushort[] registers,
        CancellationToken cancellationToken = default)
    {
        if (registers.Length == 0)
            return;

        if (_master == null || !IsConnected)
            throw new InvalidOperationException("通道未连接");

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await TrackAsync(
                () => Task.Run(
                    () => _master!.WriteMultipleRegisters(slaveId, startAddress, registers),
                    cancellationToken)).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task TrackAsync(Func<Task> send)
    {
        Interlocked.Increment(ref _txCount);
        await send().ConfigureAwait(false);
        Interlocked.Increment(ref _rxCount);
    }

    public static ushort[] EncodeValue(object value, TagDataType dataType) =>
        EncodeRegisters(value, dataType);

    private static object ParseRegisters(ushort[] registers, TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.UInt16 => registers[0],
            TagDataType.Int32 => RegistersToInt32Cdab(registers),
            TagDataType.Float32 => RegistersToFloatCdab(registers),
            TagDataType.Double64 => RegistersToDoubleGhefCdab(registers),
            _ => registers[0]
        };
    }

    private static ushort[] EncodeRegisters(object value, TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Int32 => Int32ToRegistersCdab(Convert.ToInt32(value)),
            TagDataType.Float32 => FloatToRegistersCdab(Convert.ToSingle(value)),
            TagDataType.Double64 => DoubleToRegistersGhefCdab(Convert.ToDouble(value)),
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

    private static int RegistersToInt32Cdab(ushort[] registers)
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
        return BitConverter.ToInt32(bytes, 0);
    }

    private static ushort[] Int32ToRegistersCdab(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);

        var cd = (ushort)((bytes[2] << 8) | bytes[3]);
        var ab = (ushort)((bytes[0] << 8) | bytes[1]);
        return new[] { cd, ab };
    }

    /// <summary>
    /// GHEF CDAB：四个寄存器依次为 GH、EF、CD、AB（64 位字序全反转，对应 32 位的 CDAB）。
    /// </summary>
    private static double RegistersToDoubleGhefCdab(ushort[] registers)
    {
        var bytes = new[]
        {
            (byte)(registers[3] >> 8),
            (byte)(registers[3] & 0xFF),
            (byte)(registers[2] >> 8),
            (byte)(registers[2] & 0xFF),
            (byte)(registers[1] >> 8),
            (byte)(registers[1] & 0xFF),
            (byte)(registers[0] >> 8),
            (byte)(registers[0] & 0xFF)
        };
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    private static ushort[] DoubleToRegistersGhefCdab(double value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);

        var ab = (ushort)((bytes[0] << 8) | bytes[1]);
        var cd = (ushort)((bytes[2] << 8) | bytes[3]);
        var ef = (ushort)((bytes[4] << 8) | bytes[5]);
        var gh = (ushort)((bytes[6] << 8) | bytes[7]);
        return new[] { gh, ef, cd, ab };
    }

    private static string FormatValue(object raw, TagPoint tag)
    {
        var scaled = Convert.ToDouble(raw) * tag.Scale + tag.Offset;
        var number = tag.DataType == TagDataType.Double64
            ? FormatScientific(scaled, tag.DecimalPlaces)
            : FormatNumber(scaled, tag.DecimalPlaces);
        return string.IsNullOrEmpty(tag.Unit) ? number : $"{number} {tag.Unit}";
    }

    private static string FormatScientific(double value, int places)
    {
        places = Math.Clamp(places, 0, 12);
        return value.ToString("E" + places, CultureInfo.CurrentCulture);
    }

    private static string FormatNumber(double value, int places)
    {
        var culture = CultureInfo.CurrentCulture;
        places = Math.Clamp(places, 0, 12);
        var number = value.ToString("F" + places, culture);
        if (value == 0)
            return number;

        if (!double.TryParse(number, NumberStyles.Float, culture, out var shown) || shown != 0)
            return number;

        var digits = (int)Math.Ceiling(-Math.Log10(Math.Abs(value))) + 1;
        digits = Math.Clamp(Math.Max(places, digits), 1, 12);
        number = value.ToString("F" + digits, culture);
        if (double.TryParse(number, NumberStyles.Float, culture, out shown) && shown == 0)
            return value.ToString("G6", culture);
        return number;
    }

    public void Dispose()
    {
        _master?.Dispose();
        _serialPort?.Dispose();
        _lock.Dispose();
    }
}
