namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Writes a small <c>cmap</c> table for a subset font: a format 4 subtable for the Basic Multilingual Plane and,
/// when needed, a format 12 subtable for everything.
/// </summary>
/// <remarks>
/// A PDF viewer reaches glyphs in an embedded CIDFont through the CID-to-glyph mapping, not the font's own character
/// map, so this table is for everything else that opens the font program: validators that expect one, and tools
/// that extract fonts from PDFs. It keeps the subset a self-consistent font in which each character still finds its
/// glyph.
/// </remarks>
internal static class CharacterMapWriter
{
    private const int Format4HeaderSize = 16;
    private const int Format4SegmentSize = 8;

    /// <summary>A <c>cmap</c> table for a subset, mapping each character code to its glyph.</summary>
    /// <param name="mappings">Character codes and the subset glyphs they map to.</param>
    /// <param name="symbol">
    /// True for a symbol font, whose codes are the U+F0xx values it had and go in a Windows symbol subtable.
    /// </param>
    public static byte[] Write(IEnumerable<KeyValuePair<int, ushort>> mappings, bool symbol)
    {
        KeyValuePair<int, ushort>[] sorted = mappings
            .Where(static mapping => mapping.Key is >= 0 and <= 0x10FFFF)
            .OrderBy(static mapping => mapping.Key)
            .ToArray();

        byte[]? basic = Format4(sorted.Where(static mapping => mapping.Key < 0xFFFF));
        bool needsFull = !symbol && (basic is null || sorted.Any(static mapping => mapping.Key > 0xFFFF));
        List<(ushort Platform, ushort Encoding, byte[] Subtable)> subtables = [];

        if (basic is not null)
            subtables.Add((3, symbol ? (ushort)0 : (ushort)1, basic));

        if (needsFull)
            subtables.Add((3, 10, Format12(sorted)));

        FontDataWriter table = new FontDataWriter();
        table.UInt16(0);
        table.UInt16(subtables.Count);
        int offset = 4 + (subtables.Count * 8);

        foreach ((ushort platform, ushort encoding, byte[] subtable) in subtables)
        {
            table.UInt16(platform);
            table.UInt16(encoding);
            table.UInt32((uint)offset);
            offset += subtable.Length;
        }

        foreach ((_, _, byte[] subtable) in subtables)
            table.Bytes(subtable);

        return table.ToArray();
    }

    /// <summary>
    /// A format 4 subtable of delta segments, one per run of consecutive codes mapping to consecutive glyphs; null
    /// when that many segments would overflow the format's 16-bit length.
    /// </summary>
    private static byte[]? Format4(IEnumerable<KeyValuePair<int, ushort>> mappings)
    {
        List<(int Start, int End, int Delta)> segments = [];

        foreach (KeyValuePair<int, ushort> mapping in mappings)
        {
            int delta = (mapping.Value - mapping.Key) & 0xFFFF;
            int last = segments.Count - 1;

            if (last >= 0 && segments[last].End == mapping.Key - 1 && segments[last].Delta == delta)
                segments[last] = (segments[last].Start, mapping.Key, delta);
            else
                segments.Add((mapping.Key, mapping.Key, delta));
        }

        // The format requires a final segment for 0xFFFF, mapping it to .notdef.
        segments.Add((0xFFFF, 0xFFFF, 1));

        int count = segments.Count;
        int length = Format4HeaderSize + (count * Format4SegmentSize);

        if (length > ushort.MaxValue)
            return null;

        int entrySelector = 0;

        while ((2 << entrySelector) <= count)
            entrySelector++;

        int searchRange = 2 << entrySelector;
        FontDataWriter subtable = new FontDataWriter(length);
        subtable.UInt16(4);
        subtable.UInt16(length);
        subtable.UInt16(0);
        subtable.UInt16(count * 2);
        subtable.UInt16(searchRange);
        subtable.UInt16(entrySelector);
        subtable.UInt16((count * 2) - searchRange);

        foreach ((_, int end, _) in segments)
            subtable.UInt16(end);

        subtable.UInt16(0);

        foreach ((int start, _, _) in segments)
            subtable.UInt16(start);

        foreach ((_, _, int delta) in segments)
            subtable.UInt16(delta);

        // Every segment maps by delta, so no range offsets into a glyph array.
        for (int segment = 0; segment < count; segment++)
            subtable.UInt16(0);

        return subtable.ToArray();
    }

    /// <summary>A format 12 subtable: one group per run of consecutive codes mapping to consecutive glyphs.</summary>
    private static byte[] Format12(IReadOnlyList<KeyValuePair<int, ushort>> mappings)
    {
        List<(int Start, int End, int Glyph)> groups = [];

        foreach (KeyValuePair<int, ushort> mapping in mappings)
        {
            int last = groups.Count - 1;

            if (last >= 0 && groups[last].End == mapping.Key - 1 &&
                groups[last].Glyph + (mapping.Key - groups[last].Start) == mapping.Value)
            {
                groups[last] = (groups[last].Start, mapping.Key, groups[last].Glyph);
            }
            else
            {
                groups.Add((mapping.Key, mapping.Key, mapping.Value));
            }
        }

        FontDataWriter subtable = new FontDataWriter(16 + (groups.Count * 12));
        subtable.UInt16(12);
        subtable.UInt16(0);
        subtable.UInt32((uint)(16 + (groups.Count * 12)));
        subtable.UInt32(0);
        subtable.UInt32((uint)groups.Count);

        foreach ((int start, int end, int glyph) in groups)
        {
            subtable.UInt32((uint)start);
            subtable.UInt32((uint)end);
            subtable.UInt32((uint)glyph);
        }

        return subtable.ToArray();
    }
}
