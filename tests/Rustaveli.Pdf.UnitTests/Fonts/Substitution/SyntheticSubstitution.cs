using LanguageSystemSpec = (int Required, int[] Features);
using ScriptSpec = (
    string Tag,
    (int Required, int[] Features)? Default,
    (string Tag, (int Required, int[] Features) System)[] Languages);

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

/// <summary>
/// Hand-built GSUB tables, laid out as the specification describes them, for every lookup type and format.
/// Every offset is written relative to the structure the specification measures it from.
/// </summary>
internal static class SyntheticSubstitution
{
    /// <summary>A header field to be filled with the offset of the next linked table, or 0 for a null one.</summary>
    public const int Link = -1;

    // ---- Tables --------------------------------------------------------------------------------------------------

    /// <summary>
    /// A GSUB table. Each script has an optional default language system and any number of tagged ones, each with a
    /// required feature (-1 for none) and the features it lists.
    /// </summary>
    public static byte[] Gsub(ScriptSpec[] scripts, (string Tag, int[] Lookups)[] features, byte[][] lookups)
    {
        byte[] scriptList = ScriptList(scripts);
        byte[] featureList = SyntheticLayout.FeatureList(features);
        byte[] lookupList = SyntheticLayout.LookupList(lookups);

        return new FontBytes()
            .U16(1).U16(0)
            .U16(10).U16(10 + scriptList.Length).U16(10 + scriptList.Length + featureList.Length)
            .Bytes(scriptList).Bytes(featureList).Bytes(lookupList)
            .ToArray();
    }

    /// <summary>
    /// The usual test shape: the default script's default language system lists one feature, "test", calling the
    /// lookups named — by default only the first, the others being there for contextual lookups to call.
    /// </summary>
    public static byte[] Gsub(byte[][] lookups, params int[] featureLookups) =>
        Gsub(
            [("DFLT", (-1, [0]), [])],
            [("test", featureLookups.Length == 0 ? [0] : featureLookups)],
            lookups);

    // ---- Lookups -------------------------------------------------------------------------------------------------

    public static byte[] Lookup(int type, params byte[][] subtables) => Lookup(type, 0, 0, subtables);

    /// <summary>A lookup with flags; the mark filtering set is written only when the flags call for one.</summary>
    public static byte[] Lookup(int type, int flags, int markFilteringSet, params byte[][] subtables)
    {
        bool filtered = (flags & 0x0010) != 0;
        FontBytes lookup = new FontBytes().U16(type).U16(flags).U16(subtables.Length);
        int offset = 6 + (2 * subtables.Length) + (filtered ? 2 : 0);

        foreach (byte[] subtable in subtables)
        {
            lookup.U16(offset);
            offset += subtable.Length;
        }

        if (filtered)
            lookup.U16(markFilteringSet);

        foreach (byte[] subtable in subtables)
            lookup.Bytes(subtable);

        return lookup.ToArray();
    }

    /// <summary>An extension subtable (lookup type 7) wrapping a subtable of another type.</summary>
    public static byte[] Extension(int type, byte[] subtable) => SyntheticLayout.Extension(type, subtable);

    // ---- Types 1 to 4 --------------------------------------------------------------------------------------------

    public static byte[] SingleFormat1(byte[] coverage, int delta) =>
        new FontBytes().U16(1).U16(6).I16(delta).Bytes(coverage).ToArray();

    public static byte[] SingleFormat2(byte[] coverage, params int[] substitutes)
    {
        FontBytes table = new FontBytes().U16(2).U16(6 + (2 * substitutes.Length)).U16(substitutes.Length);

        foreach (int substitute in substitutes)
            table.U16(substitute);

        return table.Bytes(coverage).ToArray();
    }

    public static byte[] Multiple(byte[] coverage, params int[][] sequences) =>
        Compose([1, Link], [coverage], sequences.Select(Glyphs).ToArray());

    public static byte[] Alternate(byte[] coverage, params int[][] alternateSets) =>
        Compose([1, Link], [coverage], alternateSets.Select(Glyphs).ToArray());

    /// <summary>Ligature sets, one per covered glyph; each ligature lists its components after the first.</summary>
    public static byte[] Ligature(byte[] coverage, params (int Glyph, int[] Components)[][] ligatureSets) =>
        Compose([1, Link], [coverage], ligatureSets.Select(LigatureSet).ToArray());

    // ---- Contextual (types 5 and 6) ------------------------------------------------------------------------------

    /// <summary>Format 1; a null rule set gets a null offset.</summary>
    public static byte[] ContextFormat1(byte[] coverage, params byte[][]?[] ruleSets) =>
        Compose([1, Link], [coverage], ruleSets.Select(RuleSet).ToArray());

    /// <summary>Format 2, rule sets indexed by the first glyph's class.</summary>
    public static byte[] ContextFormat2(byte[] coverage, byte[]? classes, params byte[][]?[] ruleSets) =>
        Compose([2, Link, Link], [coverage, classes], ruleSets.Select(RuleSet).ToArray());

    /// <summary>A rule of a format 1 or 2 context subtable: input values after the first glyph, and records.</summary>
    public static byte[] Rule(int[] input, params (int Sequence, int Lookup)[] records) =>
        Rule(input.Length + 1, input, records);

    /// <summary>A rule declaring <paramref name="glyphCount"/> input glyphs, whatever values follow.</summary>
    public static byte[] Rule(int glyphCount, int[] input, params (int Sequence, int Lookup)[] records)
    {
        FontBytes rule = new FontBytes().U16(glyphCount).U16(records.Length);

        foreach (int value in input)
            rule.U16(value);

        return Records(rule, records);
    }

    /// <summary>Format 3: a coverage table per input glyph.</summary>
    public static byte[] ContextFormat3(byte[][] input, params (int Sequence, int Lookup)[] records)
    {
        FontBytes table = new FontBytes().U16(3).U16(input.Length).U16(records.Length);
        int coverages = table.Length;

        foreach (byte[] _ in input)
            table.U16(0);

        Records(table, records);
        LinkAll(table, coverages, input);
        return table.ToArray();
    }

    public static byte[] ChainFormat1(byte[] coverage, params byte[][]?[] ruleSets) =>
        Compose([1, Link], [coverage], ruleSets.Select(RuleSet).ToArray());

    /// <summary>Format 2 of the chained type, with separate classes for backtrack, input and lookahead.</summary>
    public static byte[] ChainFormat2(
        byte[] coverage, byte[]? backtrack, byte[]? input, byte[]? lookahead, params byte[][]?[] ruleSets) =>
        Compose(
            [2, Link, Link, Link, Link], [coverage, backtrack, input, lookahead], ruleSets.Select(RuleSet).ToArray());

    /// <summary>
    /// A chained rule: backtrack values nearest first, the input values after the first glyph, lookahead values.
    /// </summary>
    public static byte[] ChainRule(
        int[] backtrack, int[] input, int[] lookahead, params (int Sequence, int Lookup)[] records) =>
        ChainRule(backtrack, input.Length + 1, input, lookahead, records);

    public static byte[] ChainRule(
        int[] backtrack, int inputCount, int[] input, int[] lookahead, params (int Sequence, int Lookup)[] records)
    {
        FontBytes rule = new FontBytes().U16(backtrack.Length);

        foreach (int value in backtrack)
            rule.U16(value);

        rule.U16(inputCount);

        foreach (int value in input)
            rule.U16(value);

        rule.U16(lookahead.Length);

        foreach (int value in lookahead)
            rule.U16(value);

        return Records(rule.U16(records.Length), records);
    }

    /// <summary>Chained format 3: coverage tables for backtrack (nearest first), input and lookahead.</summary>
    public static byte[] ChainFormat3(
        byte[][] backtrack, byte[][] input, byte[][] lookahead, params (int Sequence, int Lookup)[] records)
    {
        FontBytes table = new FontBytes().U16(3);
        int backtrackAt = WriteCount(table, backtrack.Length);
        int inputAt = WriteCount(table, input.Length);
        int lookaheadAt = WriteCount(table, lookahead.Length);
        Records(table.U16(records.Length), records);

        LinkAll(table, backtrackAt, backtrack);
        LinkAll(table, inputAt, input);
        LinkAll(table, lookaheadAt, lookahead);
        return table.ToArray();
    }

    /// <summary>Lookup type 8: coverage, backtrack (nearest first) and lookahead coverages, substitutes.</summary>
    public static byte[] ReverseChain(byte[] coverage, byte[][] backtrack, byte[][] lookahead, params int[] substitutes)
    {
        FontBytes table = new FontBytes().U16(1).U16(0);
        int backtrackAt = WriteCount(table, backtrack.Length);
        int lookaheadAt = WriteCount(table, lookahead.Length);
        table.U16(substitutes.Length);

        foreach (int substitute in substitutes)
            table.U16(substitute);

        table.SetU16(2, table.Length);
        table.Bytes(coverage);
        LinkAll(table, backtrackAt, backtrack);
        LinkAll(table, lookaheadAt, lookahead);
        return table.ToArray();
    }

    // ---- Building blocks -----------------------------------------------------------------------------------------

    /// <summary>
    /// The shape most GSUB structures share: header fields, a count and an array of offsets to children, then the
    /// tables the <see cref="Link"/> fields point to, then the children. Every offset counts from the start; a null
    /// table or child gets a null offset.
    /// </summary>
    public static byte[] Compose(int[] fields, byte[]?[] linked, byte[]?[] children)
    {
        FontBytes table = new FontBytes();

        foreach (int field in fields)
            table.U16(field == Link ? 0 : field);

        int childOffsets = table.Length + 2;
        table.U16(children.Length);

        foreach (byte[]? _ in children)
            table.U16(0);

        int link = 0;

        for (int field = 0; field < fields.Length; field++)
        {
            if (fields[field] != Link)
                continue;

            byte[]? target = linked[link++];

            if (target is null)
                continue;

            table.SetU16(field * 2, table.Length);
            table.Bytes(target);
        }

        for (int child = 0; child < children.Length; child++)
        {
            if (children[child] is not byte[] body)
                continue;

            table.SetU16(childOffsets + (child * 2), table.Length);
            table.Bytes(body);
        }

        return table.ToArray();
    }

    private static byte[] Glyphs(int[] glyphs)
    {
        FontBytes table = new FontBytes().U16(glyphs.Length);

        foreach (int glyph in glyphs)
            table.U16(glyph);

        return table.ToArray();
    }

    private static byte[] LigatureSet((int Glyph, int[] Components)[] ligatures) =>
        Compose([], [], ligatures.Select(Ligature).ToArray());

    private static byte[] Ligature((int Glyph, int[] Components) ligature)
    {
        FontBytes table = new FontBytes().U16(ligature.Glyph).U16(ligature.Components.Length + 1);

        foreach (int component in ligature.Components)
            table.U16(component);

        return table.ToArray();
    }

    private static byte[]? RuleSet(byte[][]? rules) => rules is null ? null : Compose([], [], rules);

    private static byte[] Records(FontBytes rule, (int Sequence, int Lookup)[] records)
    {
        foreach ((int sequence, int lookup) in records)
            rule.U16(sequence).U16(lookup);

        return rule.ToArray();
    }

    /// <summary>Writes a count and room for that many offsets; returns where the offsets start.</summary>
    private static int WriteCount(FontBytes table, int count)
    {
        table.U16(count);
        int at = table.Length;

        for (int index = 0; index < count; index++)
            table.U16(0);

        return at;
    }

    /// <summary>Appends the tables, pointing the offsets at <paramref name="at"/> to them.</summary>
    private static void LinkAll(FontBytes table, int at, byte[][] tables)
    {
        for (int index = 0; index < tables.Length; index++)
        {
            table.SetU16(at + (index * 2), table.Length);
            table.Bytes(tables[index]);
        }
    }

    private static byte[] ScriptList(ScriptSpec[] scripts)
    {
        FontBytes list = new FontBytes().U16(scripts.Length);
        int offset = 2 + (6 * scripts.Length);
        List<byte[]> bodies = [];

        foreach (ScriptSpec spec in scripts)
        {
            byte[] script = Script(spec.Default, spec.Languages);
            list.Tag(spec.Tag).U16(offset);
            bodies.Add(script);
            offset += script.Length;
        }

        foreach (byte[] body in bodies)
            list.Bytes(body);

        return list.ToArray();
    }

    private static byte[] Script(LanguageSystemSpec? defaultSystem, (string Tag, LanguageSystemSpec System)[] languages)
    {
        FontBytes script = new FontBytes().U16(0).U16(languages.Length);
        int records = script.Length;

        foreach ((string tag, _) in languages)
            script.Tag(tag).U16(0);

        if (defaultSystem is (int required, int[] features))
        {
            script.SetU16(0, script.Length);
            script.Bytes(LanguageSystem(required, features));
        }

        for (int index = 0; index < languages.Length; index++)
        {
            script.SetU16(records + (index * 6) + 4, script.Length);
            script.Bytes(LanguageSystem(languages[index].System.Required, languages[index].System.Features));
        }

        return script.ToArray();
    }

    private static byte[] LanguageSystem(int required, int[] features)
    {
        FontBytes system = new FontBytes().U16(0).U16(required < 0 ? 0xFFFF : required).U16(features.Length);

        foreach (int feature in features)
            system.U16(feature);

        return system.ToArray();
    }
}
