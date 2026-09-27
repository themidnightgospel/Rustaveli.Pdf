namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// An OpenType layout coverage table: the glyphs a subtable applies to, each with an index into the subtable's
/// arrays. Format 1 lists glyphs; format 2 lists ranges.
/// </summary>
internal sealed class CoverageTable
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly int _format;
    private readonly int _count;

    public CoverageTable(ReadOnlyMemory<byte> table, int offset)
    {
        ReadOnlySpan<byte> span = table.Span;
        _format = BigEndian.UInt16(span, offset);
        _count = BigEndian.UInt16(span, offset + 2);

        int recordSize = _format switch
        {
            1 => 2,
            2 => 6,
            _ => throw new FontFormatException($"Coverage format {_format} does not exist.")
        };

        _data = BigEndian.Slice(table, offset + 4L, (long)_count * recordSize);
    }

    /// <summary>The glyph's coverage index, or -1 when the glyph is not covered.</summary>
    public int IndexOf(ushort glyph)
    {
        ReadOnlySpan<byte> data = _data.Span;
        int low = 0;
        int high = _count - 1;

        while (low <= high)
        {
            int middle = (low + high) >> 1;

            if (_format == 1)
            {
                ushort candidate = BigEndian.UInt16(data, middle * 2);

                if (candidate == glyph)
                    return middle;

                if (candidate < glyph)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            else
            {
                int position = middle * 6;
                ushort start = BigEndian.UInt16(data, position);
                ushort end = BigEndian.UInt16(data, position + 2);

                if (glyph < start)
                    high = middle - 1;
                else if (glyph > end)
                    low = middle + 1;
                else
                    return BigEndian.UInt16(data, position + 4) + (glyph - start);
            }
        }

        return -1;
    }
}
