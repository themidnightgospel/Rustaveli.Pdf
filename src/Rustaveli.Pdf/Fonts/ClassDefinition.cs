namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// An OpenType layout class definition: assigns glyphs to numbered classes, so a kerning subtable can hold one value
/// per pair of classes instead of one per pair of glyphs. Every glyph not listed is in class 0.
/// </summary>
internal sealed class ClassDefinition
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly int _format;
    private readonly int _startGlyph;
    private readonly int _count;

    public ClassDefinition(ReadOnlyMemory<byte> table, int offset)
    {
        ReadOnlySpan<byte> span = table.Span;
        _format = BigEndian.UInt16(span, offset);

        switch (_format)
        {
            case 1:
                _startGlyph = BigEndian.UInt16(span, offset + 2);
                _count = BigEndian.UInt16(span, offset + 4);
                _data = BigEndian.Slice(table, offset + 6L, _count * 2L);
                break;

            case 2:
                _count = BigEndian.UInt16(span, offset + 2);
                _data = BigEndian.Slice(table, offset + 4L, _count * 6L);
                break;

            default:
                throw new FontFormatException($"Class definition format {_format} does not exist.");
        }
    }

    public int ClassOf(ushort glyph)
    {
        ReadOnlySpan<byte> data = _data.Span;

        if (_format == 1)
        {
            int index = glyph - _startGlyph;
            return index >= 0 && index < _count ? BigEndian.UInt16(data, index * 2) : 0;
        }

        int low = 0;
        int high = _count - 1;

        while (low <= high)
        {
            int middle = (low + high) >> 1;
            int position = middle * 6;
            ushort start = BigEndian.UInt16(data, position);
            ushort end = BigEndian.UInt16(data, position + 2);

            if (glyph < start)
                high = middle - 1;
            else if (glyph > end)
                low = middle + 1;
            else
                return BigEndian.UInt16(data, position + 4);
        }

        return 0;
    }
}
