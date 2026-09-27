namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Character map formats 12 and 13: sorted groups of 32-bit code ranges, covering the whole of Unicode. Format 12
/// maps each range to consecutive glyphs; format 13 maps a whole range to one glyph, as last-resort fonts do.
/// </summary>
internal sealed class SegmentedCoverageSubtable : CharacterMapSubtable
{
    private const int GroupsOffset = 16;
    private const int GroupSize = 12;
    private const int MaximumCodepoint = 0x10FFFF;

    private readonly ReadOnlyMemory<byte> _groups;
    private readonly int _count;
    private readonly bool _manyToOne;

    public SegmentedCoverageSubtable(ReadOnlyMemory<byte> data, bool manyToOne)
    {
        uint count = BigEndian.UInt32(data.Span, 12);

        // Checked against the bytes present before being used as a count, so a forged count cannot drive a loop.
        _groups = BigEndian.Slice(data, GroupsOffset, (long)count * GroupSize);
        _count = (int)count;
        _manyToOne = manyToOne;
    }

    public override ushort Lookup(int code)
    {
        if (code < 0)
            return 0;

        ReadOnlySpan<byte> groups = _groups.Span;
        int low = 0;
        int high = _count - 1;

        while (low <= high)
        {
            int middle = (low + high) >> 1;
            int position = middle * GroupSize;
            uint start = BigEndian.UInt32(groups, position);
            uint end = BigEndian.UInt32(groups, position + 4);

            if ((uint)code < start)
            {
                high = middle - 1;
            }
            else if ((uint)code > end)
            {
                low = middle + 1;
            }
            else
            {
                return Glyph(BigEndian.UInt32(groups, position + 8), (uint)code - start);
            }
        }

        return 0;
    }

    public override IEnumerable<KeyValuePair<int, ushort>> EnumerateMappings()
    {
        long previousEnd = -1;

        for (int group = 0; group < _count; group++)
        {
            int position = group * GroupSize;
            uint start = BigEndian.UInt32(_groups.Span, position);
            long end = Math.Min(BigEndian.UInt32(_groups.Span, position + 4), MaximumCodepoint);
            uint startGlyph = BigEndian.UInt32(_groups.Span, position + 8);

            for (long code = Math.Max(start, previousEnd + 1); code <= end; code++)
            {
                ushort glyph = Glyph(startGlyph, (uint)(code - start));

                if (glyph != 0)
                    yield return new KeyValuePair<int, ushort>((int)code, glyph);
            }

            previousEnd = Math.Max(previousEnd, end);
        }
    }

    private ushort Glyph(uint startGlyph, uint index)
    {
        long glyph = _manyToOne ? startGlyph : (long)startGlyph + index;

        // Glyph ids are 16-bit everywhere else in the font; a larger one cannot name a glyph that exists.
        return glyph > ushort.MaxValue ? (ushort)0 : (ushort)glyph;
    }
}
