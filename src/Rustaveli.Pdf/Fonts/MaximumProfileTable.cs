namespace Rustaveli.Pdf.Fonts;

/// <summary>The <c>maxp</c> table, read for the one value everything else is sized by: the number of glyphs.</summary>
internal sealed class MaximumProfileTable
{
    public const int NumGlyphsOffset = 4;

    public MaximumProfileTable(ReadOnlySpan<byte> data)
    {
        NumGlyphs = BigEndian.UInt16(data, NumGlyphsOffset);

        // Glyph 0 is .notdef, which every font must have: it is what a missing character is drawn with.
        if (NumGlyphs == 0)
            throw new FontFormatException("The font contains no glyphs.");
    }

    public int NumGlyphs { get; }
}
