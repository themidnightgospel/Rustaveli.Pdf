namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The header of the <c>post</c> table: italic angle, underline placement and whether the font is monospaced.
/// Glyph names, which follow the header in some versions, are not read: nothing here draws by glyph name.
/// </summary>
internal sealed class PostTable
{
    /// <summary>The header every version shares, and the whole of a version 3 table.</summary>
    public const int HeaderSize = 32;

    public PostTable(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
            throw FontFormatException.Truncated();

        ItalicAngle = BigEndian.Fixed(data, 4);
        UnderlinePosition = BigEndian.Int16(data, 8);
        UnderlineThickness = BigEndian.Int16(data, 10);
        IsFixedPitch = BigEndian.UInt32(data, 12) != 0;
    }

    /// <summary>Degrees counter-clockwise from vertical; negative for a font leaning right, as italics do.</summary>
    public float ItalicAngle { get; }

    /// <summary>The top of the underline, in font units above the baseline (usually negative).</summary>
    public short UnderlinePosition { get; }

    public short UnderlineThickness { get; }

    public bool IsFixedPitch { get; }
}
