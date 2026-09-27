namespace Rustaveli.Pdf.Fonts;

/// <summary>Character map format 6: a dense array of glyph ids for one contiguous range of codes.</summary>
internal sealed class TrimmedTableSubtable : CharacterMapSubtable
{
    private const int GlyphsOffset = 10;

    private readonly ReadOnlyMemory<byte> _glyphs;
    private readonly int _firstCode;
    private readonly int _count;

    public TrimmedTableSubtable(ReadOnlyMemory<byte> data)
    {
        ReadOnlySpan<byte> span = data.Span;
        _firstCode = BigEndian.UInt16(span, 6);
        _count = BigEndian.UInt16(span, 8);
        _glyphs = BigEndian.Slice(data, GlyphsOffset, _count * 2L);
    }

    public override ushort Lookup(int code)
    {
        int index = code - _firstCode;

        return index >= 0 && index < _count ? BigEndian.UInt16(_glyphs.Span, index * 2) : (ushort)0;
    }

    public override IEnumerable<KeyValuePair<int, ushort>> EnumerateMappings()
    {
        for (int index = 0; index < _count; index++)
        {
            ushort glyph = BigEndian.UInt16(_glyphs.Span, index * 2);

            if (glyph != 0)
                yield return new KeyValuePair<int, ushort>(_firstCode + index, glyph);
        }
    }
}
