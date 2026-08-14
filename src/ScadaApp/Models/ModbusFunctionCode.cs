namespace ScadaApp.Models;

/// <summary>
/// 本软件支持的 Modbus RTU 功能码：03 读保持寄存器、06 写单个寄存器、16 写多个寄存器
/// </summary>
public enum ModbusFunctionCode : byte
{
    ReadHoldingRegisters = 0x03,
    WriteSingleRegister = 0x06,
    WriteMultipleRegisters = 0x10
}
