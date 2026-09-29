using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.Fuzzing;

/// <summary>
/// A font file — a single face or a collection — read and used as a document would use it: every face's tables read,
/// text mapped, measured and kerned, and subsets built. The only acceptable failure is <see cref="FontFormatException"/>;
/// a subset, once made, is this library's own output, and must read back without any exception at all.
/// </summary>
internal static class FontTarget
{
    private const string Text = "Hello, World! AVATAR To P. Åéავ中\U0001D400";

    public static void Run(ReadOnlySpan<byte> input)
    {
        IReadOnlyList<OpenTypeFont> faces;

        try
        {
            faces = OpenTypeFont.LoadAll(input.ToArray());
        }
        catch (FontFormatException)
        {
            return;
        }

        foreach (OpenTypeFont face in faces)
        {
            (byte[]? trueType, byte[]? cff) subsets;

            try
            {
                subsets = Use(face);
            }
            catch (FontFormatException)
            {
                // One face may be damaged and the next intact.
                continue;
            }

            if (subsets.trueType is { } trueType)
            {
                OpenTypeFont reread = OpenTypeFont.Load(trueType);
                _ = reread.MeasureWidth(Text, 12f);
                _ = reread.Glyphs?.GetGlyphData((ushort)(reread.GlyphCount - 1));
            }

            if (subsets.cff is { } cff)
            {
                CompactFontTable reread = new CompactFontTable(cff);
                _ = reread.GetCid((ushort)(reread.GlyphCount - 1));
            }
        }
    }

    /// <summary>Uses the face every way a document does; the TrueType and CFF subsets made of it, where it has either.</summary>
    private static (byte[]? TrueType, byte[]? Cff) Use(OpenTypeFont face)
    {
        _ = face.Names.PostScriptName;
        _ = face.Os2?.WeightClass;
        _ = face.Post?.ItalicAngle;
        _ = face.Style;
        _ = face.LineMetrics;
        _ = face.Embedding.AllowsEmbedding;
        _ = face.Descriptor.Flags;

        CharacterMap map = face.CharacterMap;
        _ = map.EnumerateMappings().Count();

        foreach (char character in Text)
            map.GetGlyph(character);

        _ = face.MeasureWidth(Text, 12f, 0.5f);
        _ = face.CountFitting(Text, 12f, 60f, 0.25f);

        int sample = Math.Min(face.GlyphCount, 64);

        for (int index = 0; index < sample; index++)
        {
            ushort glyph = (ushort)(index * (face.GlyphCount / sample));
            _ = face.GetAdvance(glyph, 10f);
            _ = face.GetKerning(glyph, (ushort)((glyph + 1) % face.GlyphCount));
            _ = face.TryGetGlyphBounds(glyph, out _);
        }

        byte[]? cff = null;

        if (face.Cff is CompactFontTable table && face.TryGetTable(TableTag.Cff, out ReadOnlyMemory<byte> data))
        {
            List<(ushort Cid, ushort Glyph)> shown = [];

            for (int glyph = 0; glyph < Math.Min(table.GlyphCount, 32); glyph++)
                shown.Add((table.GetCid((ushort)glyph), (ushort)glyph));

            cff = CffSubsetter.TrySubset(data, face.UnitsPerEm, shown.DistinctBy(glyph => glyph.Cid));
        }

        if (face.Glyphs is null)
            return (null, cff);

        GlyphSubset glyphs = new GlyphSubset(face);

        foreach (char character in Text)
            glyphs.Add(face.GetGlyphId(character), character);

        for (int index = 0; index < sample; index++)
            glyphs.Add((ushort)(index * (face.GlyphCount / sample)));

        return (glyphs.Build().FontData, cff);
    }
}
