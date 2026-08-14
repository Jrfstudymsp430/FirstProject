namespace ScadaApp.Models;

/// <summary>
/// SCADA 数据点（标签），映射到 Modbus 寄存器/线圈
/// </summary>
public class TagPoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Tag1";
    public byte SlaveId { get; set; } = 1;
    public ModbusFunctionCode FunctionCode { get; set; } = ModbusFunctionCode.ReadHoldingRegisters;
    public ushort Address { get; set; }
    public TagDataType DataType { get; set; } = TagDataType.UInt16;
    public string Unit { get; set; } = string.Empty;
    public double Scale { get; set; } = 1.0;
    public double Offset { get; set; }
    public bool IsWritable { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>读取寄存器数量（Float32/Int32 需要 2 个寄存器）</summary>
    public ushort RegisterCount => DataType switch
    {
        TagDataType.Bool => 1,
        TagDataType.Int16 or TagDataType.UInt16 => 1,
        TagDataType.Int32 or TagDataType.UInt32 or TagDataType.Float32 => 2,
        _ => 1
    };
}
