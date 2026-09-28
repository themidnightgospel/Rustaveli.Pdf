namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The <c>OS/2</c> table: weight, width, style bits, embedding permissions and the typographic and Windows line
/// metrics.
/// </summary>
/// <remarks>
/// The table grew by version, and a field is read only when the table is long enough to hold it. Apple's early
/// fonts carry a 68-byte version 0 that stops before the typographic metrics.
/// </remarks>
internal sealed class Os2Table
{
    public const int FsTypeOffset = 8;

    /// <summary>The shortest table in circulation: version 0 as Apple wrote it, ending before sTypoAscender.</summary>
    private const int MinimumSize = 68;

    private const int LineMetricsEnd = 78;
    private const int Version2End = 96;

    private const ushort ItalicBit = 1 << 0;
    private const ushort UseTypoMetricsBit = 1 << 7;
    private const ushort ObliqueBit = 1 << 9;

    public Os2Table(ReadOnlySpan<byte> data)
    {
        if (data.Length < MinimumSize)
            throw FontFormatException.Truncated();

        Version = BigEndian.UInt16(data, 0);
        AverageCharWidth = BigEndian.Int16(data, 2);
        WeightClass = BigEndian.UInt16(data, 4);
        WidthClass = BigEndian.UInt16(data, 6);
        FsType = BigEndian.UInt16(data, FsTypeOffset);
        StrikeoutSize = BigEndian.Int16(data, 26);
        StrikeoutPosition = BigEndian.Int16(data, 28);
        FamilyClass = BigEndian.Int16(data, 30);
        PanoseFamilyType = data[32];
        PanoseSerifStyle = data[33];
        PanoseProportion = data[35];
        FsSelection = BigEndian.UInt16(data, 62);

        if (data.Length >= LineMetricsEnd)
        {
            HasLineMetrics = true;
            TypoAscender = BigEndian.Int16(data, 68);
            TypoDescender = BigEndian.Int16(data, 70);
            TypoLineGap = BigEndian.Int16(data, 72);
            WinAscent = BigEndian.UInt16(data, 74);
            WinDescent = BigEndian.UInt16(data, 76);
        }

        // Only version 2 and later define these; a version 0 or 1 table padded to the longer size holds garbage.
        if (Version >= 2 && data.Length >= Version2End)
        {
            XHeight = BigEndian.Int16(data, 86);
            CapHeight = BigEndian.Int16(data, 88);
        }
    }

    public ushort Version { get; }

    /// <summary>The thickness of a strike-through line, in font units.</summary>
    public short StrikeoutSize { get; }

    /// <summary>The height of a strike-through line's top above the baseline, in font units.</summary>
    public short StrikeoutPosition { get; }

    public short AverageCharWidth { get; }

    public ushort WeightClass { get; }

    public ushort WidthClass { get; }

    /// <summary>Embedding permissions; see <see cref="FontEmbedding"/>.</summary>
    public ushort FsType { get; }

    /// <summary>The IBM font class in the high byte and its subclass in the low byte.</summary>
    public short FamilyClass { get; }

    public byte PanoseFamilyType { get; }

    public byte PanoseSerifStyle { get; }

    public byte PanoseProportion { get; }

    public ushort FsSelection { get; }

    public bool IsItalic => (FsSelection & ItalicBit) != 0;

    public bool IsOblique => (FsSelection & ObliqueBit) != 0;

    /// <summary>The designer asks for the typographic metrics to be used for line spacing.</summary>
    public bool UseTypoMetrics => (FsSelection & UseTypoMetricsBit) != 0;

    /// <summary>The table is long enough to carry the typographic and Windows line metrics.</summary>
    public bool HasLineMetrics { get; }

    public short TypoAscender { get; }

    public short TypoDescender { get; }

    public short TypoLineGap { get; }

    public ushort WinAscent { get; }

    public ushort WinDescent { get; }

    /// <summary>Zero when the table predates version 2 or the font leaves it unset.</summary>
    public short XHeight { get; }

    /// <summary>Zero when the table predates version 2 or the font leaves it unset.</summary>
    public short CapHeight { get; }
}
