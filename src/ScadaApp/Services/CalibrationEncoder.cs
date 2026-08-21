using ScadaApp.Models;

namespace ScadaApp.Services;

public static class CalibrationEncoder
{
    public static ushort RegisterCount(TagDataType type) => type switch
    {
        TagDataType.Float32 or TagDataType.Int32 => 2,
        TagDataType.Double64 => 4,
        _ => 1
    };

    public static object ToTyped(double value, TagDataType type) => type switch
    {
        TagDataType.UInt16 => (ushort)Math.Clamp(
            (int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 65535),
        TagDataType.Int32 => Convert.ToInt32(
            Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), int.MinValue, int.MaxValue)),
        TagDataType.Float32 => (float)value,
        TagDataType.Double64 => value,
        _ => value
    };

    public static ushort[] EncodeTable(IReadOnlyList<CalibrationPoint> points, TagDataType type)
    {
        var registers = new List<ushort>(points.Count * 2 * RegisterCount(type));
        foreach (var point in points)
        {
            registers.AddRange(ModbusRtuClient.EncodeValue(ToTyped(point.Measured, type), type));
            registers.AddRange(ModbusRtuClient.EncodeValue(ToTyped(point.Standard, type), type));
        }

        return registers.ToArray();
    }

    public static ushort[] EncodeSlopeIntercept(double slope, double intercept, TagDataType type)
    {
        var registers = new List<ushort>(2 * RegisterCount(type));
        registers.AddRange(ModbusRtuClient.EncodeValue(ToTyped(slope, type), type));
        registers.AddRange(ModbusRtuClient.EncodeValue(ToTyped(intercept, type), type));
        return registers.ToArray();
    }

    public static bool TryFitLine(IReadOnlyList<CalibrationPoint> points, out double slope, out double intercept)
    {
        slope = 0;
        intercept = 0;
        if (points.Count < 2)
            return false;

        double sx = 0, sy = 0, sxx = 0, sxy = 0;
        var n = points.Count;
        foreach (var point in points)
        {
            sx += point.Measured;
            sy += point.Standard;
            sxx += point.Measured * point.Measured;
            sxy += point.Measured * point.Standard;
        }

        var den = n * sxx - sx * sx;
        if (Math.Abs(den) < 1e-18)
            return false;

        slope = (n * sxy - sx * sy) / den;
        intercept = (sy - slope * sx) / n;
        return true;
    }
}
