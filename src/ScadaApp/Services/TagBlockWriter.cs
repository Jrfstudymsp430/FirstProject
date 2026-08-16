using System.Globalization;
using ScadaApp.Models;

namespace ScadaApp.Services;

/// <summary>
/// 把地址连续的点表编成一段保持寄存器，供功能码 16 一次写入。
/// </summary>
public static class TagBlockWriter
{
    public const int MaxRegistersPerRequest = 120;

    public static IReadOnlyList<TagPoint> OrderByAddress(IEnumerable<TagPoint> tags) =>
        tags.OrderBy(t => t.Address).ThenBy(t => t.Name, StringComparer.Ordinal).ToList();

    public static void EnsureWritableConsecutive(IReadOnlyList<TagPoint> tags)
    {
        if (tags.Count < 2)
            throw new InvalidOperationException("请至少选择 2 个地址连续的可写点。");

        var readonlyNames = tags.Where(t => !t.IsWritable).Select(t => t.Name).ToList();
        if (readonlyNames.Count > 0)
            throw new InvalidOperationException("批量写入只能包含功能码 06/16 的可写点：" + string.Join("、", readonlyNames));

        var ordered = OrderByAddress(tags);
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            var current = ordered[i];
            var next = ordered[i + 1];
            var end = current.Address + current.RegisterCount;
            if (next.Address < end)
                throw new InvalidOperationException($"地址重叠：{current.Name}（{current.Address}）与 {next.Name}（{next.Address}）");
            if (next.Address > end)
                throw new InvalidOperationException(
                    $"地址不连续：{current.Name} 占用到 {end - 1}，{next.Name} 从 {next.Address} 开始。请选择首尾相接的点。");
        }
    }

    public static object ParseValue(TagPoint tag, string input)
    {
        input = StripUnit(input.Trim());
        return tag.DataType switch
        {
            TagDataType.UInt16 => ushort.Parse(input, CultureInfo.InvariantCulture),
            TagDataType.Float32 => float.Parse(input, CultureInfo.InvariantCulture),
            _ => input
        };
    }

    public static ushort[] EncodeBlock(IReadOnlyList<(TagPoint Tag, object Value)> items)
    {
        var ordered = items.OrderBy(i => i.Tag.Address).ToList();
        EnsureWritableConsecutive(ordered.Select(i => i.Tag).ToList());

        var registers = new List<ushort>();
        foreach (var (tag, value) in ordered)
            registers.AddRange(ModbusRtuClient.EncodeValue(value, tag.DataType));

        return registers.ToArray();
    }

    public static IEnumerable<IReadOnlyList<(TagPoint Tag, object Value)>> SplitByPduLimit(
        IReadOnlyList<(TagPoint Tag, object Value)> items)
    {
        var ordered = items.OrderBy(i => i.Tag.Address).ToList();
        var chunk = new List<(TagPoint Tag, object Value)>();
        var regs = 0;

        foreach (var item in ordered)
        {
            var count = item.Tag.RegisterCount;
            if (chunk.Count > 0 && regs + count > MaxRegistersPerRequest)
            {
                yield return chunk;
                chunk = new List<(TagPoint Tag, object Value)>();
                regs = 0;
            }

            chunk.Add(item);
            regs += count;
        }

        if (chunk.Count > 0)
            yield return chunk;
    }

    public static string StripUnit(string input)
    {
        if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return input;

        var space = input.LastIndexOf(' ');
        if (space > 0 && double.TryParse(input[..space], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return input[..space];

        return input;
    }
}
