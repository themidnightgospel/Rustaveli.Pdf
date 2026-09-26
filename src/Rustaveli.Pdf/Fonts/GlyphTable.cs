namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// TrueType outlines: the <c>glyf</c> table, addressed through the <c>loca</c> table of per-glyph offsets.
/// </summary>
/// <remarks>
/// Outlines themselves are never interpreted — a PDF viewer draws them. What is read is the part a subsetter and a
/// font descriptor need: each glyph's extent in the table, its bounding box, and, for composite glyphs, which other
/// glyphs it is built from.
/// </remarks>
internal sealed class GlyphTable
{
    private readonly ReadOnlyMemory<byte> _glyf;
    private readonly ReadOnlyMemory<byte> _loca;
    private readonly bool _longOffsets;

    public GlyphTable(ReadOnlyMemory<byte> glyf, ReadOnlyMemory<byte> loca, int glyphCount, short indexToLocFormat)
    {
        if (indexToLocFormat is not (0 or 1))
            throw new FontFormatException($"'{indexToLocFormat}' is not a glyph location format.");

        _longOffsets = indexToLocFormat == 1;
        _glyf = glyf;
        _loca = loca;
        GlyphCount = glyphCount;

        // One offset per glyph plus one marking the end of the last.
        _ = BigEndian.Slice(loca.Span, 0, (glyphCount + 1L) * (_longOffsets ? 4 : 2));
    }

    public int GlyphCount { get; }

    /// <summary>The raw data of a glyph; empty for a glyph with no outline, such as a space.</summary>
    public ReadOnlyMemory<byte> GetGlyphData(ushort glyph)
    {
        if (glyph >= GlyphCount)
            throw new ArgumentOutOfRangeException(nameof(glyph), glyph, $"The font has {GlyphCount} glyphs.");

        long start = Offset(glyph);
        long end = Offset(glyph + 1);

        if (end < start || start > _glyf.Length)
            throw new FontFormatException($"The location of glyph {glyph} is out of order or out of range.");

        // Some fonts record an end a few bytes past the table for their last glyph, a padding slip that loses
        // nothing; the glyph ends where the table does.
        end = Math.Min(end, _glyf.Length);

        if (end > start && end - start < CompositeGlyph.HeaderSize)
            throw new FontFormatException($"Glyph {glyph} is shorter than a glyph header.");

        return _glyf.Slice((int)start, (int)(end - start));
    }

    /// <summary>The glyph's bounding box, or false for a glyph with no outline.</summary>
    public bool TryGetBounds(ushort glyph, out GlyphBounds bounds)
    {
        ReadOnlySpan<byte> data = GetGlyphData(glyph).Span;

        if (data.IsEmpty)
        {
            bounds = default;
            return false;
        }

        bounds = new GlyphBounds(
            BigEndian.Int16(data, 2), BigEndian.Int16(data, 4), BigEndian.Int16(data, 6), BigEndian.Int16(data, 8));
        return true;
    }

    /// <summary>True when the glyph is built from other glyphs rather than drawn with contours of its own.</summary>
    public static bool IsComposite(ReadOnlySpan<byte> glyphData) =>
        !glyphData.IsEmpty && BigEndian.Int16(glyphData, 0) < 0;

    /// <summary>Appends the glyphs a composite glyph references; nothing for a simple or empty glyph.</summary>
    public void AddComponents(ushort glyph, List<GlyphComponent> components)
    {
        ReadOnlySpan<byte> data = GetGlyphData(glyph).Span;

        if (IsComposite(data))
            CompositeGlyph.ReadComponents(data, components);
    }

    private long Offset(int index) => _longOffsets
        ? BigEndian.UInt32(_loca.Span, index * 4)
        : BigEndian.UInt16(_loca.Span, index * 2) * 2L;
}
