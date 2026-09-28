namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Reads the component records of a composite TrueType glyph: an accented letter, say, drawn as its base letter
/// plus an accent, each a reference to another glyph.
/// </summary>
internal static class CompositeGlyph
{
    public const ushort ArgsAreWords = 0x0001;
    public const ushort HasScale = 0x0008;
    public const ushort MoreComponents = 0x0020;
    public const ushort HasXAndYScale = 0x0040;
    public const ushort HasTwoByTwo = 0x0080;

    /// <summary>On the last record: hinting instructions follow the records, their length first.</summary>
    public const ushort HasInstructions = 0x0100;

    /// <summary>The glyph header: contour count and bounding box.</summary>
    public const int HeaderSize = 10;

    /// <summary>
    /// Appends the components of <paramref name="glyph"/> to <paramref name="components"/>.
    /// </summary>
    /// <remarks>
    /// Each record's size depends on its flags: arguments are bytes or words, and a transform is absent, one
    /// scale, separate x and y scales, or a full two-by-two matrix. Misreading any of them misaligns every later
    /// record, so each is sized exactly and checked against the glyph's end. The loop always consumes at least a
    /// record's fixed part, so a glyph whose last record claims more components still terminates at its end.
    /// </remarks>
    /// <param name="glyph">A composite glyph's data, header included.</param>
    /// <param name="components">Receives the components in order.</param>
    public static void ReadComponents(ReadOnlySpan<byte> glyph, List<GlyphComponent> components) =>
        Walk(glyph, components, out _);

    /// <summary>
    /// Where the component records of <paramref name="glyph"/> end — where its instructions begin, if it has any —
    /// and where the last record's flags are.
    /// </summary>
    public static int RecordsEnd(ReadOnlySpan<byte> glyph, out int lastFlagsAt) => Walk(glyph, null, out lastFlagsAt);

    private static int Walk(ReadOnlySpan<byte> glyph, List<GlyphComponent>? components, out int lastFlagsAt)
    {
        int position = HeaderSize;
        ushort flags;

        do
        {
            lastFlagsAt = position;
            flags = BigEndian.UInt16(glyph, position);
            ushort glyphId = BigEndian.UInt16(glyph, position + 2);
            components?.Add(new GlyphComponent(glyphId, flags, position + 2));

            position += 4 + ((flags & ArgsAreWords) != 0 ? 4 : 2);

            if ((flags & HasScale) != 0)
                position += 2;
            else if ((flags & HasXAndYScale) != 0)
                position += 4;
            else if ((flags & HasTwoByTwo) != 0)
                position += 8;

            if (position > glyph.Length)
                throw FontFormatException.Truncated();
        }
        while ((flags & MoreComponents) != 0);

        return position;
    }
}
