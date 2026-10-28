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
    private const int HeaderSize = 4;
    private const int EncodingRecordSize = 8;
    private const int Format4HeaderSize = 16;
    private const int Format4SegmentSize = 8;
    private const int Format12HeaderSize = 16;
    private const int Format12GroupSize = 12;

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

        List<(int Start, int End, int Delta)>? segments = Format4Segments(sorted);
        bool needsFull = !symbol && (segments is null || sorted.Any(static mapping => mapping.Key > 0xFFFF));

        return Assemble(segments, symbol ? 0 : 1, needsFull ? Format12Groups(sorted) : null);
    }

    /// <summary>
    /// Format 4's delta segments for the codes below 0xFFFF, one per run of consecutive codes mapping to consecutive
    /// glyphs, and the final one the format requires; null when that many would overflow the format's 16-bit length.
    /// </summary>
    private static List<(int Start, int End, int Delta)>? Format4Segments(KeyValuePair<int, ushort>[] sorted)
    {
        List<(int Start, int End, int Delta)> segments = [];

        foreach (KeyValuePair<int, ushort> mapping in sorted)
        {
            if (mapping.Key >= 0xFFFF)
                break;

            int delta = (mapping.Value - mapping.Key) & 0xFFFF;
            int last = segments.Count - 1;

            if (last >= 0 && segments[last].End == mapping.Key - 1 && segments[last].Delta == delta)
                segments[last] = (segments[last].Start, mapping.Key, delta);
            else
                segments.Add((mapping.Key, mapping.Key, delta));
        }

        // The format requires a final segment for 0xFFFF, mapping it to .notdef.
        segments.Add((0xFFFF, 0xFFFF, 1));
        return Format4Length(segments.Count) > ushort.MaxValue ? null : segments;
    }

    /// <summary>Format 12's groups: one per run of consecutive codes mapping to consecutive glyphs.</summary>
    private static List<(int Start, int End, int Glyph)> Format12Groups(KeyValuePair<int, ushort>[] sorted)
    {
        List<(int Start, int End, int Glyph)> groups = [];

        foreach (KeyValuePair<int, ushort> mapping in sorted)
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

        return groups;
    }

    /// <summary>
    /// The table header and its encoding records, followed by the format 4 subtable, under
    /// <paramref name="basicEncoding"/>, and the format 12 one, each left out when null.
    /// </summary>
    private static byte[] Assemble(
        List<(int Start, int End, int Delta)>? segments,
        int basicEncoding,
        List<(int Start, int End, int Glyph)>? groups)
    {
        int basicLength = segments is null ? 0 : Format4Length(segments.Count);
        int fullLength = groups is null ? 0 : Format12Length(groups.Count);
        int subtableCount = (segments is null ? 0 : 1) + (groups is null ? 0 : 1);
        int offset = HeaderSize + (subtableCount * EncodingRecordSize);
        FontDataWriter table = new FontDataWriter(offset + basicLength + fullLength);
        table.UInt16(0);
        table.UInt16(subtableCount);

        if (segments is not null)
            EncodingRecord(table, basicEncoding, offset);

        if (groups is not null)
            EncodingRecord(table, 10, offset + basicLength);

        if (segments is not null)
            Format4(table, segments);

        if (groups is not null)
            Format12(table, groups);

        return table.DetachArray();
    }

    private static int Format4Length(int segmentCount) => Format4HeaderSize + (segmentCount * Format4SegmentSize);

    private static int Format12Length(int groupCount) => Format12HeaderSize + (groupCount * Format12GroupSize);

    /// <summary>A Windows platform record pointing at a subtable <paramref name="offset"/> bytes into the table.</summary>
    private static void EncodingRecord(FontDataWriter table, int encoding, int offset)
    {
        table.UInt16(3);
        table.UInt16(encoding);
        table.UInt32((uint)offset);
    }

    private static void Format4(FontDataWriter table, List<(int Start, int End, int Delta)> segments)
    {
        int count = segments.Count;
        int entrySelector = 0;

        while ((2 << entrySelector) <= count)
            entrySelector++;

        int searchRange = 2 << entrySelector;
        table.UInt16(4);
        table.UInt16(Format4Length(count));
        table.UInt16(0);
        table.UInt16(count * 2);
        table.UInt16(searchRange);
        table.UInt16(entrySelector);
        table.UInt16((count * 2) - searchRange);

        foreach ((_, int end, _) in segments)
            table.UInt16(end);

        table.UInt16(0);

        foreach ((int start, _, _) in segments)
            table.UInt16(start);

        foreach ((_, _, int delta) in segments)
            table.UInt16(delta);

        // Every segment maps by delta, so no range offsets into a glyph array.
        for (int segment = 0; segment < count; segment++)
            table.UInt16(0);
    }

    private static void Format12(FontDataWriter table, List<(int Start, int End, int Glyph)> groups)
    {
        table.UInt16(12);
        table.UInt16(0);
        table.UInt32((uint)Format12Length(groups.Count));
        table.UInt32(0);
        table.UInt32((uint)groups.Count);

        foreach ((int start, int end, int glyph) in groups)
        {
            table.UInt32((uint)start);
            table.UInt32((uint)end);
            table.UInt32((uint)glyph);
        }
    }
}
