using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SyntheticSubstitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

/// <summary>
/// Choosing lookups by script, language and feature; applying them in order; extensions; and the table's defences
/// against malformed and adversarial data.
/// </summary>
public class GlyphSubstitutionTableTests
{
    private static readonly FeatureTag Other = FeatureTag.Parse("othr");

    private static byte[] Cover(params int[] glyphs) => SyntheticLayout.CoverageFormat1(glyphs);

    /// <summary>Lookups each turning glyph 1 into 10 plus the lookup's index.</summary>
    private static byte[][] Lookups(int count) =>
        Enumerable.Range(0, count).Select(index => Lookup(1, SingleFormat2(Cover(1), 10 + index))).ToArray();

    private static int Shape(byte[] gsub, ScriptTag script, LanguageTag language, params FeatureSetting[] features)
    {
        GlyphBuffer buffer = Buffer(1);
        Table(gsub).Apply(buffer, script, language, features);
        return buffer.Glyphs[0];
    }

    private static int Shape(byte[] gsub, string script, string language = "dflt") =>
        Shape(gsub, ScriptTag.Parse(script), LanguageTag.Parse(language), FeatureSetting.On(Feature));

    // ---- Scripts and languages -----------------------------------------------------------------------------------

    [Fact]
    public void FallsBackFromTheScriptToTheDefaultThenLatin()
    {
        // Feature n calls lookup n; latn, DFLT and cyrl each list their own.
        byte[] all = Gsub(
            [("latn", (-1, [0]), []), ("DFLT", (-1, [1]), []), ("cyrl", (-1, [2]), [])],
            [("test", [0]), ("test", [1]), ("test", [2])],
            Lookups(3));

        byte[] withoutDefault = Gsub(
            [("latn", (-1, [0]), []), ("cyrl", (-1, [2]), [])],
            [("test", [0]), ("test", [1]), ("test", [2])],
            Lookups(3));

        byte[] cyrillicOnly = Gsub(
            [("cyrl", (-1, [2]), [])], [("test", [0]), ("test", [1]), ("test", [2])], Lookups(3));

        Assert.Equal(12, Shape(all, "cyrl"));
        Assert.Equal(10, Shape(all, "latn"));
        Assert.Equal(11, Shape(all, "grek"));
        Assert.Equal(10, Shape(withoutDefault, "grek"));
        Assert.Equal(1, Shape(cyrillicOnly, "grek"));
    }

    [Fact]
    public void ChoosesTheLanguageSystemElseTheScriptsDefault()
    {
        byte[] gsub = Gsub(
            [("cyrl", (-1, [0]), [("MKD ", (-1, [1])), ("SRB ", (-1, [2]))])],
            [("test", [0]), ("test", [1]), ("test", [2])],
            Lookups(3));

        Assert.Equal(12, Shape(gsub, "cyrl", "SRB "));
        Assert.Equal(11, Shape(gsub, "cyrl", "MKD "));
        Assert.Equal(10, Shape(gsub, "cyrl", "BGR "));
        Assert.Equal(10, Shape(gsub, "cyrl"));
    }

    [Fact]
    public void AppliesNothingForALanguageAScriptWithoutADefaultLacks()
    {
        byte[] gsub = Gsub([("cyrl", null, [("SRB ", (-1, [0]))])], [("test", [0])], Lookups(1));

        Assert.Equal(10, Shape(gsub, "cyrl", "SRB "));
        Assert.Equal(1, Shape(gsub, "cyrl", "MKD "));
    }

    [Fact]
    public void FindsALanguageInTheScriptItFellBackTo()
    {
        byte[] gsub = Gsub([("DFLT", (-1, [0]), [("SRB ", (-1, [1]))])], [("test", [0]), ("test", [1])], Lookups(2));

        Assert.Equal(11, Shape(gsub, "cyrl", "SRB "));
    }

    // ---- Features ------------------------------------------------------------------------------------------------

    [Fact]
    public void AlwaysAppliesTheRequiredFeature()
    {
        byte[] gsub = Gsub([("DFLT", (1, []), [])], [("test", [0]), ("othr", [1])], Lookups(2));

        Assert.Equal(11, Shape(gsub, ScriptTag.Default, LanguageTag.Default));
        Assert.Equal(11, Shape(gsub, ScriptTag.Default, LanguageTag.Default, FeatureSetting.Off(Other)));
    }

    [Theory]
    [InlineData(0xFFFF)]
    [InlineData(5)]
    [InlineData(1)]
    public void IgnoresARequiredFeatureThatIsNotThere(int required)
    {
        byte[] gsub = Gsub([("DFLT", (required, [0]), [])], [("test", [0])], Lookups(1));

        Assert.Equal(1, Shape(gsub, ScriptTag.Default, LanguageTag.Default));
        Assert.Equal(10, Shape(gsub, ScriptTag.Default, LanguageTag.Default, FeatureSetting.On(Feature)));
    }

    [Fact]
    public void AppliesOnlyTheFeaturesTurnedOn()
    {
        byte[] gsub = Gsub([("DFLT", (-1, [0, 1]), [])], [("test", [0]), ("othr", [1])], Lookups(2));

        Assert.Equal(1, Shape(gsub, ScriptTag.Default, LanguageTag.Default));
        Assert.Equal(10, Shape(gsub, ScriptTag.Default, LanguageTag.Default, FeatureSetting.On(Feature)));
        Assert.Equal(11, Shape(gsub, ScriptTag.Default, LanguageTag.Default, FeatureSetting.On(Other)));
        Assert.Equal(1, Shape(gsub, ScriptTag.Default, LanguageTag.Default, new FeatureSetting(Feature, 0)));
    }

    [Fact]
    public void LetsTheLastSettingOfAFeatureDecide()
    {
        byte[] gsub = Gsub([("DFLT", (-1, [0]), [])], [("test", [0])], Lookups(1));
        FeatureSetting on = FeatureSetting.On(Feature);
        FeatureSetting off = FeatureSetting.Off(Feature);

        Assert.Equal(1, Shape(gsub, ScriptTag.Default, LanguageTag.Default, on, off));
        Assert.Equal(10, Shape(gsub, ScriptTag.Default, LanguageTag.Default, off, on));
    }

    [Fact]
    public void IgnoresFeatureAndLookupIndicesPastTheirLists()
    {
        byte[] gsub = Gsub([("DFLT", (-1, [7, 0]), [])], [("test", [9, 0])], Lookups(1));

        Assert.Equal(10, Shape(gsub, ScriptTag.Default, LanguageTag.Default, FeatureSetting.On(Feature)));
        Assert.Equal(
            new[] { (0, 1) },
            Table(gsub).ResolveLookups(ScriptTag.Default, LanguageTag.Default, [FeatureSetting.On(Feature)]));
    }

    [Fact]
    public void AppliesLookupsInLookupListOrderWhateverTheFeatureOrder()
    {
        // Lookup 0 turns 1 into 2, lookup 1 turns 2 into 3; the feature calling lookup 1 comes first.
        byte[] gsub = Gsub(
            [("DFLT", (-1, [0, 1]), [])],
            [("othr", [1]), ("test", [0])],
            [Lookup(1, SingleFormat2(Cover(1), 2)), Lookup(1, SingleFormat2(Cover(2), 3))]);

        GlyphBuffer buffer = Buffer(1);
        Table(gsub).Apply(
            buffer, ScriptTag.Default, LanguageTag.Default, [FeatureSetting.On(Other), FeatureSetting.On(Feature)]);

        Assert.Equal(3, buffer.Glyphs[0]);
    }

    [Fact]
    public void GivesALookupTwoFeaturesShareTheLaterFeaturesValue()
    {
        byte[] gsub = Gsub(
            [("DFLT", (2, [0, 1]), [])],
            [("test", [0, 1]), ("othr", [1, 2]), ("reqd", [2])],
            Lookups(3));

        IReadOnlyList<(int Index, int Value)> lookups = Table(gsub).ResolveLookups(
            ScriptTag.Default, LanguageTag.Default, [new FeatureSetting(Feature, 3), new FeatureSetting(Other, 5)]);

        Assert.Equal(new[] { (0, 3), (1, 5), (2, 5) }, lookups);
    }

    [Fact]
    public void ChoosesNoLookupsWithoutScriptOrFeatureLists()
    {
        foreach (int field in new[] { 4, 6 })
        {
            byte[] gsub = SyntheticSubstitution.Gsub(Lookups(1));
            BigEndian.WriteUInt16(gsub, field, 0);

            GlyphSubstitutionTable table = Table(gsub);

            Assert.Empty(table.ResolveLookups(ScriptTag.Default, LanguageTag.Default, [FeatureSetting.On(Feature)]));
        }
    }

    [Fact]
    public void HasNoLookupsWithoutALookupList()
    {
        byte[] gsub = SyntheticSubstitution.Gsub(Lookups(1));
        BigEndian.WriteUInt16(gsub, 8, 0);
        GlyphSubstitutionTable table = Table(gsub);

        Assert.Equal(0, table.LookupCount);
        Assert.Null(table.GetLookup(0));
        Assert.Empty(table.ResolveLookups(ScriptTag.Default, LanguageTag.Default, [FeatureSetting.On(Feature)]));
    }

    [Fact]
    public void ListsTheDefaultFeaturesOfSimpleScripts()
    {
        Assert.Equal(
            new[] { "rvrn", "ccmp", "locl", "rlig", "calt", "clig", "liga" },
            GlyphSubstitutionTable.DefaultFeatures.Select(setting => setting.Tag.ToString()));
        Assert.All(GlyphSubstitutionTable.DefaultFeatures, setting => Assert.Equal(1, setting.Value));
    }

    // ---- Lookups -------------------------------------------------------------------------------------------------

    [Fact]
    public void ParsesEachLookupOnce()
    {
        GlyphSubstitutionTable table = Table(SyntheticSubstitution.Gsub(Lookups(2)));

        SubstitutionLookup first = table.GetLookup(1)!;

        Assert.Same(first, table.GetLookup(1));
        Assert.Equal(2, table.LookupCount);
        Assert.Equal(1, first.Type);
        Assert.Equal(LookupFlags.None, first.Flags);
        Assert.Single(first.Subtables);
        Assert.Null(table.GetLookup(2));
        Assert.Equal(GlyphCount, table.GlyphCount);
    }

    [Fact]
    public void SkipsLookupsPastTheListWhenAskedForThemDirectly()
    {
        GlyphBuffer buffer = Buffer(1);
        GlyphSubstitutionTable table = Table(SyntheticSubstitution.Gsub(Lookups(1)));

        table.Apply(new SubstitutionSession(table, buffer), [(3, 1), (0, 1)]);

        Assert.Equal(10, buffer.Glyphs[0]);
    }

    [Fact]
    public void FollowsExtensionsToEveryLookupType()
    {
        byte[] gsub = SyntheticSubstitution.Gsub(
        [
            Lookup(7, Extension(1, SingleFormat1(Cover(1), 1))),
            Lookup(7, Extension(2, Multiple(Cover(2), [3, 4]))),
            Lookup(7, Extension(4, Ligature(Cover(3), [(5, [4])]))),
            Lookup(7, Extension(6, ChainFormat3([Cover(9)], [Cover(5)], [], (0, 4)))),
            Lookup(1, SingleFormat1(Cover(5), 1))
        ],
        0, 1, 2, 3);

        Assert.Equal(new[] { 9, 6 }, Glyphs(Apply(gsub, [9, 1])));
    }

    [Fact]
    public void TakesAnExtensionLookupsDirectionFromItsFirstSubtable()
    {
        byte[] gsub = OneLookup(
            7,
            Extension(8, ReverseChain(Cover(1), [], [Cover(2)], 2)),
            Extension(1, SingleFormat1(Cover(7), 1)));

        SubstitutionLookup lookup = Table(gsub).GetLookup(0)!;

        Assert.Equal(SubstitutionLookup.ReverseChainingType, lookup.Type);
        Assert.Equal(new[] { 2, 2, 2, 8 }, Glyphs(Apply(gsub, [1, 1, 2, 7])));
    }

    [Fact]
    public void AppliesAReverseChainingSubtableForwardInALookupOfAnotherType()
    {
        byte[] gsub = OneLookup(
            7,
            Extension(1, SingleFormat1(Cover(7), 1)),
            Extension(8, ReverseChain(Cover(1), [], [Cover(2)], 2)));

        Assert.Equal(new[] { 1, 2, 2 }, Glyphs(Apply(gsub, [1, 1, 2])));
    }

    [Fact]
    public void ReadsTheMarkFilteringSetAfterTheSubtables()
    {
        byte[] gdef = SyntheticLayout.Gdef(SyntheticLayout.ClassFormat1(1, 1, 3, 3), null, [Cover(9), Cover(2)]);
        byte[] single = SingleFormat1(SyntheticLayout.CoverageFormat2((1, 3, 0)), 10);
        byte[] gsub = SyntheticSubstitution.Gsub([Lookup(1, 0x0010, 1, single)]);

        SubstitutionLookup lookup = Table(gsub, gdef).GetLookup(0)!;

        Assert.Equal(LookupFlags.UseMarkFilteringSet, lookup.Flags);
        Assert.Equal(new[] { 11, 12, 3 }, Glyphs(Apply(gsub, [1, 2, 3], gdef)));
    }

    // ---- Malformed and adversarial -------------------------------------------------------------------------------

    [Fact]
    public void RejectsAVersionThatDoesNotExist()
    {
        byte[] gsub = SyntheticSubstitution.Gsub(Lookups(1));
        BigEndian.WriteUInt16(gsub, 0, 2);

        FontFormatException error = Assert.Throws<FontFormatException>(() => Table(gsub));

        Assert.Contains("GSUB version 2.0", error.Message);
    }

    [Fact]
    public void AcceptsLaterMinorVersions()
    {
        byte[] gsub = SyntheticSubstitution.Gsub(Lookups(1));
        BigEndian.WriteUInt16(gsub, 2, 1);

        Assert.Equal(10, Glyphs(Apply(gsub, [1]))[0]);
    }

    [Fact]
    public void RejectsALookupListLongerThanTheTable()
    {
        byte[] gsub = SyntheticSubstitution.Gsub(Lookups(1));
        int lookupList = BigEndian.UInt16(gsub, 8);
        BigEndian.WriteUInt16(gsub, lookupList, 5000);

        Assert.Throws<FontFormatException>(() => Table(gsub));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(9, 1)]
    [InlineData(1, 3)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 4)]
    [InlineData(6, 4)]
    [InlineData(8, 2)]
    public void RejectsLookupTypesAndFormatsThatDoNotExist(int type, int format)
    {
        byte[] subtable = new FontBytes().U16(format).U16(6).U16(0).Bytes(Cover(1)).ToArray();

        FontFormatException error = Assert.Throws<FontFormatException>(() => ReadSubtable(type, subtable));

        Assert.Contains($"type {type} has no subtable format {format}", error.Message);
        Assert.Empty(Table(OneLookup(type, subtable)).GetLookup(0)!.Subtables);
        Assert.Equal(new[] { 1 }, Glyphs(Apply(OneLookup(type, subtable), [1])));
    }

    [Fact]
    public void ReadsALookupWhoseSubtableArrayOverrunsTheTableAsNothing()
    {
        byte[] gsub = SyntheticSubstitution.Gsub(Lookups(1));
        int lookupList = BigEndian.UInt16(gsub, 8);
        int lookup = lookupList + BigEndian.UInt16(gsub, lookupList + 2);
        BigEndian.WriteUInt16(gsub, lookup + 4, 5000);

        Assert.Empty(Table(gsub).GetLookup(0)!.Subtables);
        Assert.Equal(new[] { 1 }, Glyphs(Apply(gsub, [1])));
    }

    [Fact]
    public void KeepsTheSubtableAllowanceOfAMalformedLookupForTheOthers()
    {
        // Lookup 0 claims 60 000 subtables where the table holds one. Were each failed read counted against the
        // allowance, five of them would use it up and leave lookup 1 unreadable.
        byte[] gsub = SyntheticSubstitution.Gsub(Lookups(2));
        int lookupList = BigEndian.UInt16(gsub, 8);
        int lookup = lookupList + BigEndian.UInt16(gsub, lookupList + 2);
        BigEndian.WriteUInt16(gsub, lookup + 4, 60000);
        GlyphSubstitutionTable table = Table(gsub);

        for (int attempt = 0; attempt < 5; attempt++)
            Assert.Empty(table.GetLookup(0)!.Subtables);

        Assert.Single(table.GetLookup(1)!.Subtables);
    }

    [Fact]
    public void CountsTheSubtablesOfAMalformedLookupOnceHoweverOftenItIsAskedFor()
    {
        // Lookup 0 lists one subtable of a format that does not exist 16 384 times: well within the allowance once,
        // but were it read afresh at every run, seventeen runs would use the allowance up and leave lookup 1 — never
        // yet read, and intact — unreadable for the rest of the process.
        byte[] malformed = SyntheticLayout.SharedLookup(1, 16384, new FontBytes().U16(9).U16(6).U16(0).ToArray());
        byte[] gsub = SyntheticSubstitution.Gsub([malformed, Lookup(1, SingleFormat2(Cover(1), 11))], 0, 1);
        GlyphSubstitutionTable table = Table(gsub);

        for (int run = 0; run < 20; run++)
            Assert.Empty(table.GetLookup(0)!.Subtables);

        Assert.Single(table.GetLookup(1)!.Subtables);
    }

    [Fact]
    public void KeepsTheOtherLookupsOfARunWhenOneIsMalformed()
    {
        // Lookup 0 lists a subtable of a format that does not exist beside one that turns glyph 2 into 20; lookup 1
        // turns glyph 1 into 11. Only the damaged subtable is left out.
        byte[] damaged = new FontBytes().U16(9).U16(6).U16(0).ToArray();
        byte[] gsub = SyntheticSubstitution.Gsub(
            [Lookup(1, damaged, SingleFormat2(Cover(2), 20)), Lookup(1, SingleFormat2(Cover(1), 11))], 0, 1);

        Assert.Equal(new[] { 11, 20 }, Glyphs(Apply(gsub, [1, 2])));
    }

    [Fact]
    public void LeavesOutAnExtensionOfAnExtension()
    {
        byte[] gsub = OneLookup(7, Extension(7, Extension(1, SingleFormat1(Cover(1), 1))));

        Assert.Empty(Table(gsub).GetLookup(0)!.Subtables);
    }

    [Theory]
    [InlineData(0x7FFFFFF0L)]
    [InlineData(0xFFFFFFFFL)]
    public void LeavesOutAnExtensionPastTheTable(long offset)
    {
        byte[] extension = Extension(1, SingleFormat1(Cover(1), 1));
        BigEndian.WriteUInt32(extension, 4, (uint)offset);

        Assert.Empty(Table(OneLookup(7, extension)).GetLookup(0)!.Subtables);
    }

    [Fact]
    public void RejectsListsDeclaringMoreEntriesThanAFontCanUse()
    {
        // One language system lists feature 0 thousands of times, and feature 0 lists lookup 0 thousands of times:
        // a few kilobytes that would otherwise be read as hundreds of millions of entries.
        const int Repeats = 8000;
        byte[] gsub = Gsub(
            [("DFLT", (-1, Enumerable.Repeat(0, Repeats).ToArray()), [])],
            [("test", Enumerable.Repeat(0, Repeats).ToArray())],
            Lookups(1));

        GlyphSubstitutionTable table = Table(gsub);

        FontFormatException error = Assert.Throws<FontFormatException>(
            () => table.ResolveLookups(ScriptTag.Default, LanguageTag.Default, [FeatureSetting.On(Feature)]));

        Assert.Contains("more entries", error.Message);
    }

    [Fact]
    public void AcceptsAsManyEntriesAsAFontCanUse()
    {
        // 64 references to a feature of 4 095 lookups, and the 64 references themselves: exactly the limit.
        byte[] gsub = Gsub(
            [("DFLT", (-1, Enumerable.Repeat(0, 64).ToArray()), [])],
            [("test", Enumerable.Repeat(0, 4095).ToArray())],
            Lookups(1));

        Assert.Equal(10, Shape(gsub, ScriptTag.Default, LanguageTag.Default, FeatureSetting.On(Feature)));
    }

    [Fact]
    public void ReadsNothingOfSubtablesPastWhatAFontCanUse()
    {
        // Seventeen lookup list entries share one lookup listing one subtable 16 384 times: small on disk, but more
        // subtables than the limit once every entry is read — sixteen of them exactly reach it, and the seventeenth
        // is read as a lookup that substitutes nothing.
        byte[] lookup = SyntheticLayout.SharedLookup(1, 16384, SingleFormat1(Cover(1), 1));
        FontBytes lookupList = new FontBytes().U16(17);

        for (int entry = 0; entry < 17; entry++)
            lookupList.U16(2 + (17 * 2));

        lookupList.Bytes(lookup);
        byte[] scripts = new FontBytes().U16(0).ToArray();
        byte[] features = new FontBytes().U16(0).ToArray();
        byte[] gsub = new FontBytes().U16(1).U16(0).U16(10).U16(12).U16(14)
            .Bytes(scripts).Bytes(features).Bytes(lookupList.ToArray()).ToArray();

        GlyphSubstitutionTable table = Table(gsub);

        for (int index = 0; index < 16; index++)
            Assert.Equal(16384, table.GetLookup(index)!.Subtables.Count);

        Assert.Empty(table.GetLookup(16)!.Subtables);
    }

    [Fact]
    public void RejectsArgumentsThatAreMissing()
    {
        GlyphSubstitutionTable table = Table(SyntheticSubstitution.Gsub(Lookups(1)));

        Assert.Throws<ArgumentNullException>(
            () => table.Apply(null!, ScriptTag.Default, LanguageTag.Default, GlyphSubstitutionTable.DefaultFeatures));
        Assert.Throws<ArgumentNullException>(
            () => table.Apply(Buffer(1), ScriptTag.Default, LanguageTag.Default, null!));
    }

    // ---- Through the font ----------------------------------------------------------------------------------------

    [Fact]
    public void ReadsTheSubstitutionsOfAFontOnce()
    {
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("GSUB", SyntheticSubstitution.Gsub([Lookup(1, SingleFormat1(Cover(1), 1))]))
            .With("GDEF", SyntheticLayout.Gdef(SyntheticLayout.ClassFormat1(1, 1, 1)))
            .Load();

        GlyphSubstitutionTable table = font.Substitutions!;
        GlyphBuffer buffer = GlyphBuffer.FromText(font, "AB");
        table.Apply(buffer, ScriptTag.Latin, LanguageTag.Default, [FeatureSetting.On(Feature)]);

        Assert.Same(table, font.Substitutions);
        Assert.Equal(3, table.GlyphCount);
        Assert.Equal(new[] { 2, 2 }, Glyphs(buffer));
    }

    [Fact]
    public void RefusesToSubstituteAGlyphPastTheFontsLast()
    {
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("GSUB", SyntheticSubstitution.Gsub([Lookup(1, SingleFormat1(Cover(2), 1))]))
            .Load();

        GlyphBuffer buffer = GlyphBuffer.FromText(font, "B");
        font.Substitutions!.Apply(buffer, ScriptTag.Latin, LanguageTag.Default, [FeatureSetting.On(Feature)]);

        Assert.Equal(new[] { 2 }, Glyphs(buffer));
    }

    [Fact]
    public void HasNoSubstitutionsWithoutTheTable()
    {
        Assert.Null(SyntheticFont.Minimal().Load().Substitutions);
    }

    [Fact]
    public void LeavesOutAMalformedTableAsShapersDo()
    {
        // A face that gets its substitutions wrong still sets its text, only without them.
        OpenTypeFont font = SyntheticFont.Minimal().With("GSUB", [0, 1, 0]).Load();

        Assert.Null(font.Substitutions);
        Assert.Equal(new[] { 2 }, Glyphs(GlyphBuffer.FromText(font, "B")));
    }

    [Fact]
    public void LeavesOutAMalformedGlyphDefinitionTable()
    {
        OpenTypeFont font = SyntheticFont.Minimal().With("GDEF", [0, 9, 0, 0]).Load();

        Assert.Null(font.GlyphDefinitions);
    }
}
