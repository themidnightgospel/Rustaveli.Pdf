namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// One encoding subtable of the <c>cmap</c> table, mapping character codes to glyph ids.
/// </summary>
/// <remarks>
/// Subtables look characters up in the font's own bytes rather than copying them into arrays: a system-wide
/// fallback search may hold the character maps of hundreds of fonts, and the bytes are already as compact as the
/// mapping gets. Structure is validated up front, so a lookup can only miss, never fail.
/// </remarks>
internal abstract class CharacterMapSubtable
{
    /// <summary>The glyph for a character code, or 0 when the subtable does not map it.</summary>
    public abstract ushort Lookup(int code);

    /// <summary>
    /// Every mapping in the subtable, in ascending code order and with each code at most once, whatever overlaps a
    /// malformed subtable declares — so a caller walking the result is bounded by the size of the code space.
    /// </summary>
    public abstract IEnumerable<KeyValuePair<int, ushort>> EnumerateMappings();

    /// <summary>
    /// Reads the subtable at <paramref name="offset"/>, or returns null when its format is not one this library
    /// reads.
    /// </summary>
    public static CharacterMapSubtable? Read(ReadOnlyMemory<byte> table, int offset)
    {
        ReadOnlySpan<byte> span = table.Span;
        ushort format = BigEndian.UInt16(span, offset);
        ReadOnlyMemory<byte> data = table.Slice(offset);

        return format switch
        {
            0 => new ByteEncodingSubtable(data),
            4 => new SegmentMappingSubtable(data),
            6 => new TrimmedTableSubtable(data),
            12 => new SegmentedCoverageSubtable(data, manyToOne: false),
            13 => new SegmentedCoverageSubtable(data, manyToOne: true),
            _ => null
        };
    }
}
