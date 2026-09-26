namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>Hand-built OpenType tables, laid out as the specification describes them.</summary>
internal static class SyntheticTables
{
    public static byte[] Head(
        int unitsPerEm = 1000,
        (int XMin, int YMin, int XMax, int YMax) box = default,
        int macStyle = 0,
        int indexToLocFormat = 0) =>
        new FontBytes()
            .U32(0x00010000).U32(0x00010000).U32(0).U32(0x5F0F3CF5).U16(0).U16(unitsPerEm)
            .Zeros(16)
            .I16(box.XMin).I16(box.YMin).I16(box.XMax).I16(box.YMax)
            .U16(macStyle).U16(8).I16(2).I16(indexToLocFormat).I16(0)
            .ToArray();

    public static byte[] Hhea(
        int ascender = 800,
        int descender = -200,
        int lineGap = 0,
        int numberOfHMetrics = 1,
        int advanceWidthMax = 700) =>
        new FontBytes()
            .U32(0x00010000).I16(ascender).I16(descender).I16(lineGap).U16(advanceWidthMax)
            .I16(0).I16(0).I16(0).I16(1).I16(0).I16(0).Zeros(8).I16(0).U16(numberOfHMetrics)
            .ToArray();

    /// <summary>Version 1.0 for TrueType outlines, 0.5 (just the glyph count) for CFF.</summary>
    public static byte[] Maxp(int numGlyphs, bool trueType = true) => trueType
        ? new FontBytes().U32(0x00010000).U16(numGlyphs).Zeros(26).ToArray()
        : new FontBytes().U32(0x00005000).U16(numGlyphs).ToArray();

    public static byte[] Hmtx(params (int Advance, int Bearing)[] metrics) => Hmtx(metrics, []);

    /// <summary>Full metrics, then bearings for the glyphs sharing the last advance.</summary>
    public static byte[] Hmtx((int Advance, int Bearing)[] metrics, int[] bearings)
    {
        FontBytes table = new FontBytes();

        foreach ((int advance, int bearing) in metrics)
            table.U16(advance).I16(bearing);

        foreach (int bearing in bearings)
            table.I16(bearing);

        return table.ToArray();
    }

    public static byte[] Os2(
        int version = 4,
        int weight = 400,
        int width = 5,
        int fsType = 0,
        int familyClass = 0,
        byte[]? panose = null,
        int fsSelection = 0,
        (int Ascender, int Descender, int LineGap) typo = default,
        (int Ascent, int Descent) win = default,
        int xHeight = 0,
        int capHeight = 0,
        int? length = null)
    {
        FontBytes table = new FontBytes()
            .U16(version).I16(450).U16(weight).U16(width).U16(fsType)
            .Zeros(20)
            .I16(familyClass)
            .Bytes(panose ?? new byte[10])
            .Zeros(16)
            .Tag("TEST")
            .U16(fsSelection).U16(0x20).U16(0xFFFF)
            .I16(typo.Ascender).I16(typo.Descender).I16(typo.LineGap)
            .U16(win.Ascent).U16(win.Descent)
            .U32(1).U32(0)
            .I16(xHeight).I16(capHeight).U16(0).U16(32).U16(2);

        byte[] bytes = table.ToArray();
        return length is int cut ? bytes.Take(cut).ToArray() : bytes;
    }

    public static byte[] Post(double italicAngle = 0, int underlinePosition = -100, int underlineThickness = 50,
        bool fixedPitch = false) =>
        new FontBytes()
            .U32(0x00030000).Fixed(italicAngle).I16(underlinePosition).I16(underlineThickness)
            .U32(fixedPitch ? 1 : 0).Zeros(16)
            .ToArray();

    /// <summary>A name record whose string is given as raw bytes.</summary>
    public static (int Platform, int Encoding, int Language, int NameId, byte[] Bytes) NameRecord(
        int platform, int encoding, int language, int nameId, string text) =>
        (platform, encoding, language, nameId,
            platform == 1
                ? text.Select(character => (byte)character).ToArray()
                : new FontBytes().Utf16(text).ToArray());

    public static byte[] Name(params (int Platform, int Encoding, int Language, int NameId, byte[] Bytes)[] records)
    {
        FontBytes table = new FontBytes().U16(0).U16(records.Length).U16(6 + (12 * records.Length));
        int offset = 0;

        foreach ((int platform, int encoding, int language, int nameId, byte[] bytes) in records)
        {
            table.U16(platform).U16(encoding).U16(language).U16(nameId).U16(bytes.Length).U16(offset);
            offset += bytes.Length;
        }

        foreach ((_, _, _, _, byte[] bytes) in records)
            table.Bytes(bytes);

        return table.ToArray();
    }

    public static byte[] Cmap(params (int Platform, int Encoding, byte[] Subtable)[] subtables)
    {
        FontBytes table = new FontBytes().U16(0).U16(subtables.Length);
        int offset = 4 + (8 * subtables.Length);

        foreach ((int platform, int encoding, byte[] subtable) in subtables)
        {
            table.U16(platform).U16(encoding).U32(offset);
            offset += subtable.Length;
        }

        foreach ((_, _, byte[] subtable) in subtables)
            table.Bytes(subtable);

        return table.ToArray();
    }

    public static byte[] Format0(params (int Code, int Glyph)[] mappings)
    {
        byte[] glyphs = new byte[256];

        foreach ((int code, int glyph) in mappings)
            glyphs[code] = (byte)glyph;

        return new FontBytes().U16(0).U16(262).U16(0).Bytes(glyphs).ToArray();
    }

    /// <summary>A format 4 subtable with one delta segment per mapping, plus the final 0xFFFF segment.</summary>
    public static byte[] Format4(params (int Code, int Glyph)[] mappings) =>
        Format4Segments(mappings
            .OrderBy(mapping => mapping.Code)
            .Select(mapping => (mapping.Code, mapping.Code, (mapping.Glyph - mapping.Code) & 0xFFFF, (int[]?)null))
            .ToArray());

    /// <summary>
    /// A format 4 subtable from explicit segments; a segment with glyphs maps through the glyph array (with its delta
    /// added), the others by delta alone.
    /// </summary>
    public static byte[] Format4Segments(params (int Start, int End, int Delta, int[]? Glyphs)[] segments)
    {
        List<(int Start, int End, int Delta, int[]? Glyphs)> all = [.. segments, (0xFFFF, 0xFFFF, 1, null)];
        int count = all.Count;
        FontBytes table = new FontBytes().U16(4).U16(0).U16(0).U16(count * 2).U16(0).U16(0).U16(0);

        foreach ((_, int end, _, _) in all)
            table.U16(end);

        table.U16(0);

        foreach ((int start, _, _, _) in all)
            table.U16(start);

        foreach ((_, _, int delta, _) in all)
            table.U16(delta);

        // Range offsets count from their own position to the segment's first glyph-array entry.
        int arrayPosition = 0;

        for (int index = 0; index < count; index++)
        {
            int[]? glyphs = all[index].Glyphs;

            if (glyphs is null)
            {
                table.U16(0);
                continue;
            }

            table.U16(((count - index) * 2) + (arrayPosition * 2));
            arrayPosition += glyphs.Length;
        }

        foreach ((_, _, _, int[]? glyphs) in all)
        {
            foreach (int glyph in glyphs ?? [])
                table.U16(glyph);
        }

        table.SetU16(2, Math.Min(table.Length, 0xFFFF));
        return table.ToArray();
    }

    public static byte[] Format6(int firstCode, params int[] glyphs)
    {
        FontBytes table = new FontBytes().U16(6).U16(10 + (2 * glyphs.Length)).U16(0).U16(firstCode).U16(glyphs.Length);

        foreach (int glyph in glyphs)
            table.U16(glyph);

        return table.ToArray();
    }

    /// <summary>Format 12 (a range to consecutive glyphs) or 13 (a range to one glyph).</summary>
    public static byte[] Format12(bool manyToOne = false, params (long Start, long End, long Glyph)[] groups)
    {
        FontBytes table = new FontBytes()
            .U16(manyToOne ? 13 : 12).U16(0).U32(16 + (12 * groups.Length)).U32(0).U32(groups.Length);

        foreach ((long start, long end, long glyph) in groups)
            table.U32(start).U32(end).U32(glyph);

        return table.ToArray();
    }

    /// <summary>A minimal simple glyph: a header with its bounding box and a few bytes standing in for an outline.</summary>
    public static byte[] SimpleGlyph(int xMin, int yMin, int xMax, int yMax) =>
        new FontBytes().I16(1).I16(xMin).I16(yMin).I16(xMax).I16(yMax).U16(0).U16(0).U8(1).U8(0).ToArray();

    /// <summary>A composite glyph; each component is its flags, glyph and the argument and transform bytes.</summary>
    public static byte[] CompositeGlyph(
        (int XMin, int YMin, int XMax, int YMax) box, params (int Flags, int Glyph, byte[] Arguments)[] components)
    {
        FontBytes glyph = new FontBytes().I16(-1).I16(box.XMin).I16(box.YMin).I16(box.XMax).I16(box.YMax);

        foreach ((int flags, int glyphId, byte[] arguments) in components)
            glyph.U16(flags).U16(glyphId).Bytes(arguments);

        return glyph.ToArray();
    }

    /// <summary>The glyf and loca tables for the glyphs, each glyph padded to four bytes.</summary>
    public static (byte[] Glyf, byte[] Loca) GlyphData(bool longOffsets, params byte[][] glyphs)
    {
        FontBytes glyf = new FontBytes();
        FontBytes loca = new FontBytes();

        foreach (byte[] glyph in glyphs)
        {
            WriteOffset(loca, glyf.Length, longOffsets);
            glyf.Bytes(glyph).Align(4);
        }

        WriteOffset(loca, glyf.Length, longOffsets);
        return (glyf.ToArray(), loca.ToArray());
    }

    /// <summary>A Windows (version 0) kern table.</summary>
    public static byte[] KernWindows(params (int Coverage, (int Left, int Right, int Value)[] Pairs)[] subtables)
    {
        FontBytes table = new FontBytes().U16(0).U16(subtables.Length);

        foreach ((int coverage, (int Left, int Right, int Value)[] pairs) in subtables)
        {
            table.U16(0).U16(14 + (6 * pairs.Length)).U16(coverage);
            WritePairs(table, pairs);
        }

        return table.ToArray();
    }

    /// <summary>An Apple (version 1.0) kern table.</summary>
    public static byte[] KernApple(params (int Coverage, (int Left, int Right, int Value)[] Pairs)[] subtables)
    {
        FontBytes table = new FontBytes().U32(0x00010000).U32(subtables.Length);

        foreach ((int coverage, (int Left, int Right, int Value)[] pairs) in subtables)
        {
            table.U32(16 + (6 * pairs.Length)).U16(coverage).U16(0);
            WritePairs(table, pairs);
        }

        return table.ToArray();
    }

    private static void WritePairs(FontBytes table, (int Left, int Right, int Value)[] pairs)
    {
        table.U16(pairs.Length).U16(0).U16(0).U16(0);

        foreach ((int left, int right, int value) in pairs.OrderBy(pair => (pair.Left << 16) | pair.Right))
            table.U16(left).U16(right).I16(value);
    }

    private static void WriteOffset(FontBytes loca, int offset, bool longOffsets)
    {
        if (longOffsets)
            loca.U32(offset);
        else
            loca.U16(offset / 2);
    }
}
