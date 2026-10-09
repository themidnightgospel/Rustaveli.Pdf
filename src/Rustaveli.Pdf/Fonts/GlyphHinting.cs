namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// TrueType hinting — the instructions that fit a glyph's outline to a screen's pixel grid at small sizes — taken out
/// of the glyphs a document embeds, and the few fonts that cannot do without it.
/// </summary>
/// <remarks>
/// <para>
/// PDF viewers mostly set type with their own smoothing and print at resolutions where hinting changes nothing, yet
/// the instructions and the programs they call are often most of a small subset's bytes. Taken out, the outlines are
/// untouched: only the instructions go.
/// </para>
/// <para>
/// A handful of East Asian fonts build their glyphs from strokes that the instructions put in place, and are
/// unreadable without them. They are known by their family names, and always keep their hinting.
/// </para>
/// </remarks>
internal static class GlyphHinting
{
    /// <summary>Parts of the family names of fonts whose glyphs are assembled by their instructions.</summary>
    private static readonly string[] AssembledByInstructions =
    [
        "cpop", "DFGirl-W6-WIN-BF", "DFGothic-EB", "DFGyoSho-Lt", "DFHei", "DFHSGothic-W5", "DFHSMincho-W3",
        "DFHSMincho-W7", "DFKaiSho-SB", "DFKaiShu", "DFKai-SB", "DFMing", "DLC", "HuaTianKaiTi?", "HuaTianSongTi?",
        "Ming(for ISO10646)", "MingLiU", "MingMedium", "PMingLiU", "MingLi43",
    ];

    /// <summary>Whether the font named <paramref name="names"/> draws its glyphs wrongly without its hinting.</summary>
    public static bool IsNeededBy(FontNames names) =>
        AssembledByInstructions.Any(part =>
            names.Family.Contains(part, StringComparison.Ordinal) || names.PreferredFamily.Contains(part, StringComparison.Ordinal));

    /// <summary>
    /// Appends <paramref name="glyph"/> to <paramref name="output"/> without its instructions, outline unchanged; a
    /// glyph cut short is refused before anything is written.
    /// </summary>
    /// <param name="glyph">A glyph's data as the <c>glyf</c> table holds it, header included; not empty.</param>
    /// <param name="output">The table being written.</param>
    public static void StripInto(ReadOnlySpan<byte> glyph, FontDataWriter output)
    {
        if (glyph.Length < CompositeGlyph.HeaderSize)
            throw FontFormatException.Truncated();

        if (BigEndian.Int16(glyph, 0) < 0)
            StripComposite(glyph, output);
        else
            StripSimple(glyph, output);
    }

    /// <summary>
    /// A simple glyph holds its instructions between its contours' end points and its points' flags, their length
    /// first: the length becomes 0 and the instructions go.
    /// </summary>
    private static void StripSimple(ReadOnlySpan<byte> glyph, FontDataWriter output)
    {
        int contours = BigEndian.Int16(glyph, 0);
        int lengthAt = CompositeGlyph.HeaderSize + (2 * contours);

        // A glyph of no contours may end at its header, as FreeType and fontTools accept: it has no points for
        // instructions to move, and no instructions to take out.
        if (contours == 0 && lengthAt + 2 > glyph.Length)
        {
            output.Bytes(glyph);
            return;
        }

        if (lengthAt + 2 > glyph.Length)
            throw FontFormatException.Truncated();

        int instructions = BigEndian.UInt16(glyph, lengthAt);
        int pointsAt = lengthAt + 2 + instructions;

        if (pointsAt > glyph.Length)
            throw FontFormatException.Truncated();

        output.Bytes(glyph.Slice(0, lengthAt));
        output.UInt16(0);
        output.Bytes(glyph.Slice(pointsAt));
    }

    /// <summary>
    /// A composite glyph's instructions follow its last component record, which says so in its flags: the records
    /// are kept, with that flag cleared.
    /// </summary>
    private static void StripComposite(ReadOnlySpan<byte> glyph, FontDataWriter output)
    {
        int end = CompositeGlyph.RecordsEnd(glyph, out int lastFlagsAt);
        ushort flags = BigEndian.UInt16(glyph, lastFlagsAt);
        int start = output.Length;

        output.Bytes(glyph.Slice(0, end));
        output.PatchUInt16(start + lastFlagsAt, flags & ~CompositeGlyph.HasInstructions);
    }
}
