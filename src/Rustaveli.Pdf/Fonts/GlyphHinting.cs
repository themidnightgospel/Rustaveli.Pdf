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

    /// <summary><paramref name="glyph"/> without its instructions, outline unchanged.</summary>
    /// <param name="glyph">A glyph's data as the <c>glyf</c> table holds it, header included; not empty.</param>
    public static byte[] Strip(ReadOnlySpan<byte> glyph)
    {
        if (glyph.Length < CompositeGlyph.HeaderSize)
            throw FontFormatException.Truncated();

        return BigEndian.Int16(glyph, 0) < 0 ? StripComposite(glyph) : StripSimple(glyph);
    }

    /// <summary>
    /// A simple glyph holds its instructions between its contours' end points and its points' flags, their length
    /// first: the length becomes 0 and the instructions go.
    /// </summary>
    private static byte[] StripSimple(ReadOnlySpan<byte> glyph)
    {
        int contours = BigEndian.Int16(glyph, 0);
        int lengthAt = CompositeGlyph.HeaderSize + (2 * contours);

        // A glyph of no contours may end at its header, as FreeType and fontTools accept: it has no points for
        // instructions to move, and no instructions to take out.
        if (contours == 0 && lengthAt + 2 > glyph.Length)
            return glyph.ToArray();

        if (lengthAt + 2 > glyph.Length)
            throw FontFormatException.Truncated();

        int instructions = BigEndian.UInt16(glyph, lengthAt);
        int pointsAt = lengthAt + 2 + instructions;

        if (pointsAt > glyph.Length)
            throw FontFormatException.Truncated();

        byte[] stripped = new byte[glyph.Length - instructions];
        glyph.Slice(0, lengthAt).CopyTo(stripped);
        BigEndian.WriteUInt16(stripped, lengthAt, 0);
        glyph.Slice(pointsAt).CopyTo(stripped.AsSpan(lengthAt + 2));
        return stripped;
    }

    /// <summary>
    /// A composite glyph's instructions follow its last component record, which says so in its flags: the records
    /// are kept, with that flag cleared.
    /// </summary>
    private static byte[] StripComposite(ReadOnlySpan<byte> glyph)
    {
        int end = CompositeGlyph.RecordsEnd(glyph, out int lastFlagsAt);
        byte[] stripped = glyph.Slice(0, end).ToArray();
        ushort flags = BigEndian.UInt16(stripped, lastFlagsAt);
        BigEndian.WriteUInt16(stripped, lastFlagsAt, (ushort)(flags & ~CompositeGlyph.HasInstructions));
        return stripped;
    }
}
