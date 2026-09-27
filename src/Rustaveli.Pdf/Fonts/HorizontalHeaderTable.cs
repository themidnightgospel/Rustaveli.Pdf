namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The <c>hhea</c> table: the font's horizontal line metrics and how many glyphs carry their own advance.
/// </summary>
internal sealed class HorizontalHeaderTable
{
    public const int Size = 36;

    public const int AdvanceWidthMaxOffset = 10;

    public const int NumberOfHMetricsOffset = 34;

    public HorizontalHeaderTable(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
            throw FontFormatException.Truncated();

        Ascender = BigEndian.Int16(data, 4);
        Descender = BigEndian.Int16(data, 6);
        LineGap = BigEndian.Int16(data, 8);
        AdvanceWidthMax = BigEndian.UInt16(data, AdvanceWidthMaxOffset);
        NumberOfHMetrics = BigEndian.UInt16(data, NumberOfHMetricsOffset);
    }

    public short Ascender { get; }

    /// <summary>Negative below the baseline, as the font stores it.</summary>
    public short Descender { get; }

    public short LineGap { get; }

    public ushort AdvanceWidthMax { get; }

    public ushort NumberOfHMetrics { get; }
}
