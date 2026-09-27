namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Pair adjustment format 1: for each first glyph, a sorted list of second glyphs and their values.
/// </summary>
internal sealed class GlyphPairAdjustment : PairAdjustment
{
    private readonly int _pairSetCount;
    private readonly int _recordSize;

    public GlyphPairAdjustment(ReadOnlyMemory<byte> table, int offset)
        : base(table, offset)
    {
        _pairSetCount = BigEndian.UInt16(table.Span, offset + 8);
        _ = BigEndian.Slice(table.Span, offset + 10L, _pairSetCount * 2L);
        _recordSize = 2 + FirstValueSize + SecondValueSize;
    }

    public override bool TryGetAdjustment(ushort left, ushort right, out int adjustment)
    {
        adjustment = 0;
        int index = Coverage.IndexOf(left);

        if (index < 0 || index >= _pairSetCount)
            return false;

        ReadOnlySpan<byte> span = Table.Span;
        int pairSet = Offset + BigEndian.UInt16(span, Offset + 10 + (index * 2));
        int low = 0;
        int high = BigEndian.UInt16(span, pairSet) - 1;

        while (low <= high)
        {
            int middle = (low + high) >> 1;
            int record = pairSet + 2 + (middle * _recordSize);
            ushort candidate = BigEndian.UInt16(span, record);

            if (candidate == right)
            {
                adjustment = ReadXAdvance(record + 2);
                return true;
            }

            if (candidate < right)
                low = middle + 1;
            else
                high = middle - 1;
        }

        // Unlike a class-based subtable, a glyph-pair subtable that lacks the pair does not apply to it, and the
        // lookup's next subtable is consulted.
        return false;
    }
}
