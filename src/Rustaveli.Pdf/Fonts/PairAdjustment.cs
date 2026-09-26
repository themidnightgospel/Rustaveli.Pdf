namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// A GPOS pair adjustment subtable (lookup type 2), read for the horizontal advance it gives the first glyph of a
/// pair — which is what kerning is.
/// </summary>
/// <remarks>
/// Only the first glyph's x-advance is used. A pair can also shift the second glyph (its x-placement) or change its
/// advance; left-to-right kerning does neither, and there is no way to express a shift of one glyph alone in the
/// single inter-glyph adjustment a PDF text operator takes. Device tables, which fine-tune values per pixel size,
/// are for screen rasterisation and are ignored.
/// </remarks>
internal abstract class PairAdjustment
{
    private const ushort XAdvanceBit = 0x0004;

    protected PairAdjustment(ReadOnlyMemory<byte> table, int offset)
    {
        ReadOnlySpan<byte> span = table.Span;
        Table = table;
        Offset = offset;
        Coverage = new CoverageTable(table, offset + BigEndian.UInt16(span, offset + 2));

        ushort first = BigEndian.UInt16(span, offset + 4);
        ushort second = BigEndian.UInt16(span, offset + 6);
        FirstValueSize = ValueRecordSize(first);
        SecondValueSize = ValueRecordSize(second);
        XAdvanceOffset = (first & XAdvanceBit) == 0 ? -1 : 2 * CountBits(first & 0x0003);
    }

    protected ReadOnlyMemory<byte> Table { get; }

    protected int Offset { get; }

    protected CoverageTable Coverage { get; }

    protected int FirstValueSize { get; }

    protected int SecondValueSize { get; }

    /// <summary>Where the x-advance sits within the first value record, or -1 when the record omits it.</summary>
    protected int XAdvanceOffset { get; }

    /// <summary>
    /// True when the subtable applies to the pair, even with a zero value: a shaper then stops looking at the
    /// lookup's later subtables, and so must this.
    /// </summary>
    public abstract bool TryGetAdjustment(ushort left, ushort right, out int adjustment);

    public static PairAdjustment Read(ReadOnlyMemory<byte> table, int offset)
    {
        ushort format = BigEndian.UInt16(table.Span, offset);

        return format switch
        {
            1 => new GlyphPairAdjustment(table, offset),
            2 => new ClassPairAdjustment(table, offset),
            _ => throw new FontFormatException($"Pair adjustment format {format} does not exist.")
        };
    }

    /// <summary>The x-advance from the first value record starting at <paramref name="record"/>.</summary>
    protected int ReadXAdvance(int record) =>
        XAdvanceOffset < 0 ? 0 : BigEndian.Int16(Table.Span, record + XAdvanceOffset);

    /// <summary>Value records hold one 16-bit field per bit set in the low byte of their format.</summary>
    private static int ValueRecordSize(ushort format) => 2 * CountBits(format & 0x00FF);

    private static int CountBits(int value)
    {
        int count = 0;

        for (; value != 0; value &= value - 1)
            count++;

        return count;
    }
}
