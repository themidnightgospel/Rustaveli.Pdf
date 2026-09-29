namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The <c>cmap</c> table, read through the one subtable that best maps Unicode to the font's glyphs.
/// </summary>
/// <remarks>
/// Preference follows what platforms themselves use: the full-repertoire Unicode subtables first, so characters
/// outside the Basic Multilingual Plane map; then the Windows BMP subtable every Windows font has; then other
/// Unicode-platform subtables; then a Windows symbol map, and a Macintosh Roman map as the last resort.
/// </remarks>
internal sealed class CharacterMap
{
    private const int RecordSize = 8;
    private const int Unusable = int.MaxValue;

    /// <summary>
    /// Glyphs for code points below 256, looked up once. Latin text is the overwhelming majority of what gets
    /// measured, and this turns each of its characters into an array read instead of a binary search.
    /// </summary>
    private readonly ushort[] _latin = new ushort[256];

    private readonly CharacterMapSubtable? _subtable;
    private readonly int _glyphCount;

    /// <summary>Reads the best Unicode subtable of a font's <c>cmap</c> table.</summary>
    /// <param name="table">The <c>cmap</c> table.</param>
    /// <param name="glyphCount">The font's glyph count; a mapping to a glyph beyond it counts as no mapping.</param>
    public CharacterMap(ReadOnlyMemory<byte> table, int glyphCount)
    {
        ReadOnlySpan<byte> span = table.Span;
        int count = BigEndian.UInt16(span, 2);
        _ = BigEndian.Slice(span, 4, (long)count * RecordSize);
        _glyphCount = glyphCount;

        int bestRank = Unusable;
        FontFormatException? unreadable = null;

        for (int index = 0; index < count; index++)
        {
            int record = 4 + (index * RecordSize);
            ushort platform = BigEndian.UInt16(span, record);
            ushort encoding = BigEndian.UInt16(span, record + 2);
            uint offset = BigEndian.UInt32(span, record + 4);
            int rank = Rank(platform, encoding);

            if (rank >= bestRank)
                continue;

            CharacterMapSubtable? subtable;

            // A subtable that cannot be read is passed over for the next best, as FreeType passes over one that
            // fails its checks; only when none can be read is the font's character map reported as malformed.
            try
            {
                if (offset >= (uint)table.Length)
                    throw FontFormatException.Truncated();

                subtable = CharacterMapSubtable.Read(table, (int)offset);
            }
            catch (FontFormatException exception)
            {
                unreadable = exception;
                continue;
            }

            if (subtable is null)
                continue;

            bestRank = rank;
            _subtable = subtable;
            Encoding = rank switch
            {
                SymbolRank => CharacterEncoding.Symbol,
                MacRomanRank => CharacterEncoding.MacRoman,
                _ => CharacterEncoding.Unicode
            };
        }

        if (_subtable is null && unreadable is not null)
            throw unreadable;

        for (int codepoint = 0; codepoint < _latin.Length; codepoint++)
            _latin[codepoint] = LookupUncached(codepoint);
    }

    private const int SymbolRank = 5;
    private const int MacRomanRank = 6;

    public CharacterEncoding Encoding { get; }

    /// <summary>The glyph for a Unicode code point, or 0 (.notdef) when the font does not have one.</summary>
    public ushort GetGlyph(int codepoint) =>
        codepoint is >= 0 and < 256 ? _latin[codepoint] : LookupUncached(codepoint);

    /// <summary>
    /// Every character the font maps, each once, as Unicode code points — except in a symbol font, whose codes are
    /// reported as the U+F0xx values it stores. Ascending, other than for a Mac Roman map, whose byte order is not
    /// Unicode order.
    /// </summary>
    public IEnumerable<KeyValuePair<int, ushort>> EnumerateMappings()
    {
        if (_subtable is null)
            yield break;

        foreach (KeyValuePair<int, ushort> mapping in _subtable.EnumerateMappings())
        {
            if (mapping.Value >= _glyphCount)
                continue;

            if (Encoding != CharacterEncoding.MacRoman)
            {
                yield return mapping;
            }
            else if (mapping.Key < 256)
            {
                yield return new KeyValuePair<int, ushort>(MacRoman.ToUnicode((byte)mapping.Key), mapping.Value);
            }
        }
    }

    private ushort LookupUncached(int codepoint)
    {
        if (_subtable is null)
            return 0;

        ushort glyph;

        switch (Encoding)
        {
            case CharacterEncoding.MacRoman:
                int code = MacRoman.Encode(codepoint);
                glyph = code < 0 ? (ushort)0 : _subtable.Lookup(code);
                break;

            case CharacterEncoding.Symbol:
                glyph = _subtable.Lookup(codepoint);

                if (glyph == 0 && codepoint is >= 0 and < 256)
                    glyph = _subtable.Lookup(0xF000 + codepoint);

                break;

            default:
                glyph = _subtable.Lookup(codepoint);
                break;
        }

        return glyph < _glyphCount ? glyph : (ushort)0;
    }

    private static int Rank(ushort platform, ushort encoding) => (platform, encoding) switch
    {
        (3, 10) => 0,
        (0, 4) or (0, 6) => 1,
        (3, 1) => 2,
        (0, 3) => 3,
        (0, 0) or (0, 1) or (0, 2) => 4,
        (3, 0) => SymbolRank,
        (1, 0) => MacRomanRank,
        _ => Unusable
    };
}
