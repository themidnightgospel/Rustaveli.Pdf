namespace Rustaveli.Pdf.Fonts;

/// <summary>Character map format 0: one byte per code for codes 0 to 255, found in old Macintosh fonts.</summary>
internal sealed class ByteEncodingSubtable : CharacterMapSubtable
{
    private const int GlyphsOffset = 6;
    private const int CodeCount = 256;

    private readonly ReadOnlyMemory<byte> _glyphs;

    public ByteEncodingSubtable(ReadOnlyMemory<byte> data)
    {
        _glyphs = BigEndian.Slice(data, GlyphsOffset, CodeCount);
    }

    public override ushort Lookup(int code) => code is >= 0 and < CodeCount ? _glyphs.Span[code] : (ushort)0;

    public override IEnumerable<KeyValuePair<int, ushort>> EnumerateMappings()
    {
        for (int code = 0; code < CodeCount; code++)
        {
            ushort glyph = _glyphs.Span[code];

            if (glyph != 0)
                yield return new KeyValuePair<int, ushort>(code, glyph);
        }
    }
}
