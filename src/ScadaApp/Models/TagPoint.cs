namespace ScadaApp.Models;

/// <summary>
/// SCADA 数据点。采集一律用 03；06/16 表示可写。Float32 / Int32 为 CDAB，Double64 为 GHEF CDAB。
/// </summary>
public class TagPoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Tag1";
    public byte SlaveId { get; set; } = 1;
    public ModbusFunctionCode FunctionCode { get; set; } = ModbusFunctionCode.ReadHoldingRegisters;
    public ushort Address { get; set; }
    public TagDataType DataType { get; set; } = TagDataType.Float32;
    public string Unit { get; set; } = string.Empty;
    public double Scale { get; set; } = 1.0;
    public double Offset { get; set; }
    public int DecimalPlaces { get; set; } = 2;
    public bool TrendEnabled { get; set; } = true;
    public bool IsEnabled { get; set; } = true;

    public bool IsWritable => FunctionCode is ModbusFunctionCode.WriteSingleRegister
        or ModbusFunctionCode.WriteMultipleRegisters;

    public ushort RegisterCount => DataType switch
    {
        TagDataType.Float32 or TagDataType.Int32 => 2,
        TagDataType.Double64 => 4,
        _ => 1
    };
}
