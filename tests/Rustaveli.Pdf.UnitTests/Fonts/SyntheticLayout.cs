namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>Hand-built GPOS kerning structures and CFF tables.</summary>
internal static class SyntheticLayout
{
    /// <summary>XAdvance alone, the usual kerning value record.</summary>
    public const int XAdvance = 0x0004;

    /// <summary>XPlacement and XAdvance: the advance is not the first field, so its offset must be computed.</summary>
    public const int PlacementAndAdvance = 0x0005;

    /// <summary>
    /// A GPOS table. A script with null features has no default language system; a required feature of -1 means
    /// none.
    /// </summary>
    public static byte[] Gpos(
        (string Tag, int[]? Features, int Required)[] scripts,
        (string Tag, int[] Lookups)[] features,
        (int Type, byte[][] Subtables)[] lookups) =>
        GposOfLookups(scripts, features, lookups.Select(lookup => Lookup(lookup.Type, lookup.Subtables)).ToArray());

    /// <summary>A GPOS table over lookups already built, such as by <see cref="SharedLookup"/>.</summary>
    public static byte[] GposOfLookups(
        (string Tag, int[]? Features, int Required)[] scripts, (string Tag, int[] Lookups)[] features, byte[][] lookups)
    {
        byte[] scriptList = ScriptList(scripts);
        byte[] featureList = FeatureList(features);
        byte[] lookupList = LookupList(lookups);

        return new FontBytes()
            .U16(1).U16(0)
            .U16(10).U16(10 + scriptList.Length).U16(10 + scriptList.Length + featureList.Length)
            .Bytes(scriptList).Bytes(featureList).Bytes(lookupList)
            .ToArray();
    }

    /// <summary>The usual shape: the default script's kern feature pointing at the given lookups.</summary>
    public static byte[] KernGpos(params (int Type, byte[][] Subtables)[] lookups) =>
        Gpos(
            [("DFLT", [0], -1)],
            [("kern", Enumerable.Range(0, lookups.Length).ToArray())],
            lookups);

    public static byte[] CoverageFormat1(params int[] glyphs)
    {
        FontBytes table = new FontBytes().U16(1).U16(glyphs.Length);

        foreach (int glyph in glyphs)
            table.U16(glyph);

        return table.ToArray();
    }

    public static byte[] CoverageFormat2(params (int Start, int End, int StartIndex)[] ranges)
    {
        FontBytes table = new FontBytes().U16(2).U16(ranges.Length);

        foreach ((int start, int end, int index) in ranges)
            table.U16(start).U16(end).U16(index);

        return table.ToArray();
    }

    public static byte[] ClassFormat1(int startGlyph, params int[] classes)
    {
        FontBytes table = new FontBytes().U16(1).U16(startGlyph).U16(classes.Length);

        foreach (int value in classes)
            table.U16(value);

        return table.ToArray();
    }

    public static byte[] ClassFormat2(params (int Start, int End, int Class)[] ranges)
    {
        FontBytes table = new FontBytes().U16(2).U16(ranges.Length);

        foreach ((int start, int end, int value) in ranges)
            table.U16(start).U16(end).U16(value);

        return table.ToArray();
    }

    /// <summary>
    /// Pair adjustment format 1. Each pair set lists second glyphs with their first and second value records, given
    /// as the fields their formats call for.
    /// </summary>
    public static byte[] PairFormat1(
        byte[] coverage,
        int valueFormat1,
        int valueFormat2,
        params (int Second, int[] Value1, int[] Value2)[][] pairSets)
    {
        FontBytes table = new FontBytes().U16(1).U16(0).U16(valueFormat1).U16(valueFormat2).U16(pairSets.Length);
        int offset = 10 + (2 * pairSets.Length);
        List<byte[]> bodies = [];

        foreach ((int Second, int[] Value1, int[] Value2)[] pairs in pairSets)
        {
            FontBytes body = new FontBytes().U16(pairs.Length);

            foreach ((int second, int[] value1, int[] value2) in pairs)
            {
                body.U16(second);

                foreach (int field in value1.Concat(value2))
                    body.I16(field);
            }

            table.U16(offset);
            bodies.Add(body.ToArray());
            offset += body.Length;
        }

        foreach (byte[] body in bodies)
            table.Bytes(body);

        table.SetU16(2, table.Length);
        return table.Bytes(coverage).ToArray();
    }

    /// <summary>Pair adjustment format 2: one record per pair of classes, row by row.</summary>
    public static byte[] PairFormat2(
        byte[] coverage,
        byte[] firstClasses,
        byte[] secondClasses,
        int valueFormat1,
        int valueFormat2,
        int firstClassCount,
        int secondClassCount,
        params int[][] records)
    {
        FontBytes table = new FontBytes()
            .U16(2).U16(0).U16(valueFormat1).U16(valueFormat2).U16(0).U16(0)
            .U16(firstClassCount).U16(secondClassCount);

        foreach (int[] record in records)
        {
            foreach (int field in record)
                table.I16(field);
        }

        table.SetU16(2, table.Length);
        table.Bytes(coverage);
        table.SetU16(8, table.Length);
        table.Bytes(firstClasses);
        table.SetU16(10, table.Length);
        return table.Bytes(secondClasses).ToArray();
    }

    /// <summary>An extension subtable (lookup type 9) wrapping a subtable of another type.</summary>
    public static byte[] Extension(int type, byte[] subtable) =>
        new FontBytes().U16(1).U16(type).U32(8).Bytes(subtable).ToArray();

    /// <summary>
    /// A CFF table with <paramref name="glyphCount"/> empty charstrings. A CID-keyed one carries a ROS operator and,
    /// when given, a charset (its format byte included).
    /// </summary>
    public static byte[] Cff(
        string name, int glyphCount, bool cidKeyed = false, byte[]? charset = null, byte[]? extraDict = null)
    {
        byte[] prefix = extraDict ?? [];
        int dictLength = prefix.Length + (cidKeyed ? 5 : 0) + (charset is null ? 0 : 6) + 6;

        int header = 4;
        int nameIndex = 2 + 1 + 2 + name.Length;
        int dictIndex = 2 + 1 + 2 + dictLength;
        int emptyIndexes = 4;
        int charsetOffset = header + nameIndex + dictIndex + emptyIndexes;
        int charStringsOffset = charsetOffset + (charset?.Length ?? 0);

        FontBytes dict = new FontBytes().Bytes(prefix);

        if (cidKeyed)
            dict.U8(139).U8(139).U8(139).U8(12).U8(30);

        if (charset is not null)
            dict.U8(29).U32(charsetOffset).U8(15);

        dict.U8(29).U32(charStringsOffset).U8(17);

        FontBytes table = new FontBytes()
            .U8(1).U8(0).U8(4).U8(4)
            .U16(1).U8(1).U8(1).U8(1 + name.Length).Tag(name)
            .U16(1).U8(1).U8(1).U8(1 + dictLength).Bytes(dict.ToArray())
            .U16(0)
            .U16(0)
            .Bytes(charset ?? []);

        table.U16(glyphCount).U8(1);

        for (int glyph = 0; glyph <= glyphCount; glyph++)
            table.U8(1 + glyph);

        for (int glyph = 0; glyph < glyphCount; glyph++)
            table.U8(14);

        return table.ToArray();
    }

    private static byte[] ScriptList((string Tag, int[]? Features, int Required)[] scripts)
    {
        FontBytes list = new FontBytes().U16(scripts.Length);
        int offset = 2 + (6 * scripts.Length);
        List<byte[]> bodies = [];

        foreach ((string tag, int[]? features, int required) in scripts)
        {
            FontBytes script = new FontBytes().U16(features is null ? 0 : 4).U16(0);

            if (features is not null)
            {
                script.U16(0).U16(required < 0 ? 0xFFFF : required).U16(features.Length);

                foreach (int feature in features)
                    script.U16(feature);
            }

            list.Tag(tag).U16(offset);
            bodies.Add(script.ToArray());
            offset += script.Length;
        }

        foreach (byte[] body in bodies)
            list.Bytes(body);

        return list.ToArray();
    }

    private static byte[] FeatureList((string Tag, int[] Lookups)[] features)
    {
        FontBytes list = new FontBytes().U16(features.Length);
        int offset = 2 + (6 * features.Length);
        List<byte[]> bodies = [];

        foreach ((string tag, int[] lookups) in features)
        {
            FontBytes feature = new FontBytes().U16(0).U16(lookups.Length);

            foreach (int lookup in lookups)
                feature.U16(lookup);

            list.Tag(tag).U16(offset);
            bodies.Add(feature.ToArray());
            offset += feature.Length;
        }

        foreach (byte[] body in bodies)
            list.Bytes(body);

        return list.ToArray();
    }

    public static byte[] Lookup(int type, byte[][] subtables)
    {
        FontBytes lookup = new FontBytes().U16(type).U16(0).U16(subtables.Length);
        int subtableOffset = 6 + (2 * subtables.Length);

        foreach (byte[] subtable in subtables)
        {
            lookup.U16(subtableOffset);
            subtableOffset += subtable.Length;
        }

        foreach (byte[] subtable in subtables)
            lookup.Bytes(subtable);

        return lookup.ToArray();
    }

    /// <summary>A lookup listing one subtable <paramref name="count"/> times, as an adversarial font might.</summary>
    public static byte[] SharedLookup(int type, int count, byte[] subtable)
    {
        FontBytes lookup = new FontBytes().U16(type).U16(0).U16(count);

        for (int index = 0; index < count; index++)
            lookup.U16(6 + (2 * count));

        return lookup.Bytes(subtable).ToArray();
    }

    private static byte[] LookupList(byte[][] lookups)
    {
        FontBytes list = new FontBytes().U16(lookups.Length);
        int offset = 2 + (2 * lookups.Length);

        foreach (byte[] lookup in lookups)
        {
            list.U16(offset);
            offset += lookup.Length;
        }

        foreach (byte[] lookup in lookups)
            list.Bytes(lookup);

        return list.ToArray();
    }
}
