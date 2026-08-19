using ScadaApp.Models;

namespace ScadaApp.Services;

/// <summary>
/// 把点表按从机和地址拼成尽量少的 03 读保持寄存器块。
/// 跨度不超过 125 个寄存器时一次读完；再宽才拆包。
/// </summary>
public static class TagBlockReader
{
    public const int MaxRegistersPerRequest = 125;

    public static IReadOnlyList<TagReadBlock> Split(IEnumerable<TagPoint> tags)
    {
        var blocks = new List<TagReadBlock>();
        foreach (var group in tags.GroupBy(t => t.SlaveId).OrderBy(g => g.Key))
        {
            TagReadBlockBuilder? current = null;
            foreach (var tag in group.OrderBy(t => t.Address).ThenBy(t => t.Name, StringComparer.Ordinal))
            {
                var tagEnd = (int)tag.Address + tag.RegisterCount;
                if (current == null)
                {
                    current = new TagReadBlockBuilder(group.Key, tag.Address, tagEnd, tag);
                    continue;
                }

                var newStart = Math.Min(current.Start, tag.Address);
                var newEnd = Math.Max(current.End, tagEnd);
                if (newEnd - newStart > MaxRegistersPerRequest)
                {
                    blocks.Add(current.Build());
                    current = new TagReadBlockBuilder(group.Key, tag.Address, tagEnd, tag);
                }
                else
                {
                    current.Add(tag, newStart, newEnd);
                }
            }

            if (current != null)
                blocks.Add(current.Build());
        }

        return blocks;
    }

    private sealed class TagReadBlockBuilder
    {
        private readonly byte _slaveId;
        private readonly List<TagPoint> _tags = new();

        public TagReadBlockBuilder(byte slaveId, int start, int end, TagPoint first)
        {
            _slaveId = slaveId;
            Start = start;
            End = end;
            _tags.Add(first);
        }

        public int Start { get; private set; }
        public int End { get; private set; }

        public void Add(TagPoint tag, int start, int end)
        {
            _tags.Add(tag);
            Start = start;
            End = end;
        }

        public TagReadBlock Build() =>
            new(_slaveId, (ushort)Start, (ushort)(End - Start), _tags);
    }
}

public sealed record TagReadBlock(
    byte SlaveId,
    ushort Start,
    ushort Count,
    IReadOnlyList<TagPoint> Tags);
