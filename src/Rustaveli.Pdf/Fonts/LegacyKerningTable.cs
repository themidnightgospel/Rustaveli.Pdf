namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The legacy <c>kern</c> table's format 0 subtables: sorted lists of glyph pairs with a horizontal adjustment each.
/// Read in both the Windows layout (16-bit header) and Apple's (32-bit header).
/// </summary>
/// <remarks>
/// Only subtables that kern horizontally along the baseline are used. Cross-stream subtables move glyphs up or
/// down, and minimum-value subtables constrain justification; neither changes the distance a pair is set apart.
/// A subtable's pair count is trusted over its length field, which overflows for any subtable above 64 KB and is
/// wrong in many fonts, but never beyond the end of the table.
/// </remarks>
internal sealed class LegacyKerningTable : KerningSource
{
    private const int PairSize = 6;
    private const int MaximumSubtables = 4096;

    private readonly ReadOnlyMemory<byte> _data;
    private readonly List<(int Pairs, int Count, bool Overrides)> _subtables = [];

    public LegacyKerningTable(ReadOnlyMemory<byte> data)
    {
        _data = data;
        ReadOnlySpan<byte> span = data.Span;

        if (BigEndian.UInt16(span, 0) == 0)
            ReadWindowsSubtables(span);
        else if (BigEndian.UInt32(span, 0) == 0x00010000)
            ReadAppleSubtables(span);
        else
            throw new FontFormatException("The kern table has an unknown version.");
    }

    /// <summary>True when the table has at least one subtable the font's horizontal kerning comes from.</summary>
    public bool HasPairs => _subtables.Count > 0;

    public override int GetAdjustment(ushort left, ushort right)
    {
        uint key = ((uint)left << 16) | right;
        int total = 0;

        foreach ((int pairs, int count, bool overrides) in _subtables)
        {
            int? value = Find(_data.Span, pairs, count, key);

            if (value is int found)
                total = overrides ? found : total + found;
        }

        return total;
    }

    private void ReadWindowsSubtables(ReadOnlySpan<byte> span)
    {
        int count = BigEndian.UInt16(span, 2);
        int position = 4;

        for (int index = 0; index < count && position < span.Length; index++)
        {
            int length = BigEndian.UInt16(span, position + 2);
            ushort coverage = BigEndian.UInt16(span, position + 4);

            bool horizontal = (coverage & 0x1) != 0;
            bool minimum = (coverage & 0x2) != 0;
            bool crossStream = (coverage & 0x4) != 0;

            if ((coverage >> 8) == 0 && horizontal && !minimum && !crossStream)
                AddFormat0(span, position + 6, overrides: (coverage & 0x8) != 0);

            // A length too short to cover even the header cannot locate the next subtable; stop rather than loop.
            if (length < 6)
                break;

            position += length;
        }
    }

    private void ReadAppleSubtables(ReadOnlySpan<byte> span)
    {
        uint count = BigEndian.UInt32(span, 4);
        long position = 8;

        for (uint index = 0; index < count && position < span.Length; index++)
        {
            uint length = BigEndian.UInt32(span, (int)position);
            ushort coverage = BigEndian.UInt16(span, (int)position + 4);

            // Vertical, cross-stream and variation subtables in the high bits; the format in the low byte.
            if ((coverage & 0xE0FF) == 0)
                AddFormat0(span, (int)position + 8, overrides: false);

            if (length < 8)
                break;

            position += length;
        }
    }

    private void AddFormat0(ReadOnlySpan<byte> span, int body, bool overrides)
    {
        int declared = BigEndian.UInt16(span, body);
        int available = (span.Length - (body + 8)) / PairSize;
        int count = Math.Min(declared, Math.Max(0, available));

        if (count <= 0)
            return;

        // Every subtable is searched for every pair measured; see GlyphPositioningKerning.MaximumSubtables.
        if (_subtables.Count == MaximumSubtables)
            throw new FontFormatException("The kern table has more subtables than a font can use.");

        _subtables.Add((body + 8, count, overrides));
    }

    private static int? Find(ReadOnlySpan<byte> span, int pairs, int count, uint key)
    {
        int low = 0;
        int high = count - 1;

        while (low <= high)
        {
            int middle = (low + high) >> 1;
            int position = pairs + (middle * PairSize);
            uint candidate = BigEndian.UInt32(span, position);

            if (candidate == key)
                return BigEndian.Int16(span, position + 4);

            if (candidate < key)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return null;
    }
}
