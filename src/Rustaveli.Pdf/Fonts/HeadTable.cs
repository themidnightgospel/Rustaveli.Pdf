namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The <c>head</c> table: units per em, the font-wide bounding box and the format of the <c>loca</c> table.
/// </summary>
internal sealed class HeadTable
{
    public const int Size = 54;

    /// <summary>Offset of the whole-font checksum adjustment, which a subsetter must rewrite.</summary>
    public const int ChecksumAdjustmentOffset = 8;

    public const int BoundingBoxOffset = 36;

    public const int IndexToLocFormatOffset = 50;

    private const ushort BoldBit = 1 << 0;
    private const ushort ItalicBit = 1 << 1;

    public HeadTable(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
            throw FontFormatException.Truncated();

        UnitsPerEm = BigEndian.UInt16(data, 18);

        // The specification allows 16 to 16 384. Zero would divide every scaled metric by zero; values outside the
        // range are corrupt data rather than an unusual design, and scaling by them yields nonsense.
        if (UnitsPerEm < 16 || UnitsPerEm > 16384)
            throw new FontFormatException($"The font declares {UnitsPerEm} units per em.");

        XMin = BigEndian.Int16(data, BoundingBoxOffset);
        YMin = BigEndian.Int16(data, BoundingBoxOffset + 2);
        XMax = BigEndian.Int16(data, BoundingBoxOffset + 4);
        YMax = BigEndian.Int16(data, BoundingBoxOffset + 6);
        MacStyle = BigEndian.UInt16(data, 44);
        IndexToLocFormat = BigEndian.Int16(data, IndexToLocFormatOffset);
    }

    public int UnitsPerEm { get; }

    public short XMin { get; }

    public short YMin { get; }

    public short XMax { get; }

    public short YMax { get; }

    public ushort MacStyle { get; }

    public bool IsBold => (MacStyle & BoldBit) != 0;

    public bool IsItalic => (MacStyle & ItalicBit) != 0;

    /// <summary>0 when <c>loca</c> stores halved 16-bit offsets, 1 when it stores 32-bit offsets.</summary>
    public short IndexToLocFormat { get; }
}
