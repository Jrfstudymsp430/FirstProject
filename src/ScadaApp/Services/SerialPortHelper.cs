using System.IO.Ports;

namespace ScadaApp.Services;

public static class SerialPortHelper
{
    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public static int[] CommonBaudRates { get; } = { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };

    public static int[] DataBitsOptions { get; } = { 7, 8 };

    public static Parity[] ParityOptions { get; } =
        { Parity.None, Parity.Odd, Parity.Even, Parity.Mark, Parity.Space };

    public static StopBits[] StopBitsOptions { get; } =
        { StopBits.One, StopBits.OnePointFive, StopBits.Two };
}
