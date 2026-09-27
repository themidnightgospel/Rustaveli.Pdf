namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// Everything a PDF FontDescriptor needs, in the font's own units. <see cref="ToGlyphSpace"/> converts to the
/// thousandths of an em that PDF font dictionaries use.
/// </summary>
internal sealed class FontDescriptorInfo
{
    private FontDescriptorInfo(OpenTypeFont font)
    {
        Os2Table? os2 = font.Os2;
        PostTable? post = font.Post;
        HeadTable head = font.Head;
        LineMetrics lines = font.LineMetrics;

        UnitsPerEm = font.UnitsPerEm;
        FontName = font.Names.PostScriptName;
        FamilyName = font.Names.PreferredFamily;
        BoundingBox = new GlyphBounds(head.XMin, head.YMin, head.XMax, head.YMax);
        ItalicAngle = post?.ItalicAngle ?? 0f;
        Ascent = lines.Ascent;
        Descent = -lines.Descent;
        Leading = lines.LineGap;
        Weight = font.Style.Weight;
        Width = font.Style.Width;
        AverageWidth = os2?.AverageCharWidth ?? 0;
        MaxWidth = font.HorizontalHeader.AdvanceWidthMax;
        MissingWidth = font.GetAdvance(0);
        CapHeight = os2 is { CapHeight: > 0 } ? os2.CapHeight : GlyphTop(font, 'H') ?? Ascent;
        XHeight = os2 is { XHeight: > 0 } ? os2.XHeight : GlyphTop(font, 'x') ?? 0;
        StemV = (int)Math.Round((50 + Math.Pow(Weight / 65.0, 2)) * UnitsPerEm / 1000.0);
        Flags = ComputeFlags(font, os2, post);
    }

    /// <summary>The BaseFont and FontName: the PostScript name, before any subset tag.</summary>
    public string FontName { get; }

    public string FamilyName { get; }

    public FontFlags Flags { get; }

    /// <summary>The font-wide bounding box from <c>head</c>.</summary>
    public GlyphBounds BoundingBox { get; }

    /// <summary>Degrees counter-clockwise from vertical; negative for right-leaning italics.</summary>
    public float ItalicAngle { get; }

    /// <summary>The ascent of <see cref="OpenTypeFont.LineMetrics"/>, positive.</summary>
    public int Ascent { get; }

    /// <summary>The descent of <see cref="OpenTypeFont.LineMetrics"/>, negative as PDF expects.</summary>
    public int Descent { get; }

    public int Leading { get; }

    /// <summary>From <c>OS/2</c>, else the top of the "H" glyph, else the ascent.</summary>
    public int CapHeight { get; }

    /// <summary>From <c>OS/2</c>, else the top of the "x" glyph, else 0.</summary>
    public int XHeight { get; }

    /// <summary>
    /// The dominant vertical stem width. Estimated from the weight class, 50 + (weight / 65)² thousandths of an
    /// em, because measuring stems means interpreting outlines; viewers use it only to embolden or substitute, and
    /// the estimate is the one other PDF writers use.
    /// </summary>
    public int StemV { get; }

    public int AverageWidth { get; }

    public int MaxWidth { get; }

    /// <summary>The advance of glyph 0, drawn for characters the font lacks.</summary>
    public int MissingWidth { get; }

    public int Weight { get; }

    /// <summary>The <c>OS/2</c> width class, 1 to 9, 5 being normal.</summary>
    public int Width { get; }

    public int UnitsPerEm { get; }

    /// <summary>A font-unit value in PDF glyph space, where the em is 1000 units.</summary>
    public float ToGlyphSpace(int fontUnits) => fontUnits * 1000f / UnitsPerEm;

    internal static FontDescriptorInfo Create(OpenTypeFont font) => new FontDescriptorInfo(font);

    private static int? GlyphTop(OpenTypeFont font, char character)
    {
        ushort glyph = font.GetGlyphId(character);

        return glyph != 0 && font.Glyphs is GlyphTable glyphs && glyphs.TryGetBounds(glyph, out GlyphBounds bounds)
            ? bounds.YMax
            : null;
    }

    /// <summary>
    /// The descriptor flags. Serif and script come from the IBM family class where the font sets one, else from
    /// PANOSE. A font is symbolic, as PDF defines it, when it maps any character outside the Adobe standard Latin
    /// character set — which includes every font with Greek, Cyrillic or Georgian — or uses a symbol encoding.
    /// </summary>
    private static FontFlags ComputeFlags(OpenTypeFont font, Os2Table? os2, PostTable? post)
    {
        FontFlags flags = FontFlags.None;

        if (post?.IsFixedPitch == true || os2 is { PanoseFamilyType: 2, PanoseProportion: 9 })
            flags |= FontFlags.FixedPitch;

        int familyClass = (os2?.FamilyClass ?? 0) >> 8;

        if (familyClass is 1 or 2 or 3 or 4 or 5 or 7)
            flags |= FontFlags.Serif;
        else if (familyClass == 10)
            flags |= FontFlags.Script;
        else if (familyClass == 0 && os2 is { PanoseFamilyType: 2, PanoseSerifStyle: >= 2 and <= 10 })
            flags |= FontFlags.Serif;
        else if (familyClass == 0 && os2 is { PanoseFamilyType: 3 })
            flags |= FontFlags.Script;

        if (font.Style.Slant != FontSlant.Upright || post is { ItalicAngle: not 0f })
            flags |= FontFlags.Italic;

        flags |= IsSymbolic(font.CharacterMap) ? FontFlags.Symbolic : FontFlags.Nonsymbolic;

        return flags;
    }

    private static bool IsSymbolic(CharacterMap map)
    {
        if (map.Encoding is CharacterEncoding.Symbol or CharacterEncoding.None)
            return true;

        foreach (KeyValuePair<int, ushort> mapping in map.EnumerateMappings())
        {
            if (!IsStandardLatin(mapping.Key))
                return true;
        }

        return false;
    }

    /// <summary>
    /// The Adobe standard Latin character set: every character of the standard, Mac Roman, WinAnsi and PDFDoc
    /// encodings. Control characters are included because fonts map them to empty glyphs without adding anything
    /// to their repertoire.
    /// </summary>
    private static bool IsStandardLatin(int codepoint) => codepoint switch
    {
        <= 0xFF => true,
        0x0131 or 0x0141 or 0x0142 or 0x0152 or 0x0153 or 0x0160 or 0x0161 or 0x0178 or 0x017D or 0x017E => true,
        0x0192 or 0x02C6 or 0x02C7 or 0x02D8 or 0x02D9 or 0x02DA or 0x02DB or 0x02DC or 0x02DD => true,
        0x2013 or 0x2014 or 0x2018 or 0x2019 or 0x201A or 0x201C or 0x201D or 0x201E => true,
        0x2020 or 0x2021 or 0x2022 or 0x2026 or 0x2030 or 0x2039 or 0x203A or 0x2044 => true,
        0x20AC or 0x2122 or 0x2212 or 0xFB01 or 0xFB02 => true,
        _ => false
    };
}
