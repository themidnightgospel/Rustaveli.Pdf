using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

public class KerningTests
{
    private static GlyphPositioningKerning Gpos(params (int Type, byte[][] Subtables)[] lookups) =>
        new GlyphPositioningKerning(SyntheticLayout.KernGpos(lookups));

    private static byte[] Pairs(byte[] coverage, params (int Second, int[] Value1, int[] Value2)[][] sets) =>
        SyntheticLayout.PairFormat1(coverage, SyntheticLayout.XAdvance, 0, sets);

    // ---- Legacy kern table -------------------------------------------------------------------------------------

    [Fact]
    public void ReadsTheLegacyKernTableOfACommittedFont()
    {
        OpenTypeFont font = TestFonts.SpecimenRegular;

        Assert.IsType<LegacyKerningTable>(font.Kerning);
        Assert.Equal(-40, font.GetKerning(font.GetGlyphId('A'), font.GetGlyphId('V')));
        Assert.Equal(-70, font.GetKerning(font.GetGlyphId('T'), font.GetGlyphId('o')));
        Assert.Equal(-130, font.GetKerning(font.GetGlyphId('P'), font.GetGlyphId('.')));
        Assert.Equal(0, font.GetKerning(font.GetGlyphId('a'), font.GetGlyphId('b')));
    }

    [Fact]
    public void SumsWindowsSubtablesUnlessOneOverrides()
    {
        LegacyKerningTable kern = new LegacyKerningTable(SyntheticTables.KernWindows(
            (0x0001, [(1, 2, -30), (2, 1, -10)]),
            (0x0001, [(1, 2, -5)]),
            (0x0009, [(2, 1, 7)])));

        Assert.True(kern.HasPairs);
        Assert.Equal(-35, kern.GetAdjustment(1, 2));
        Assert.Equal(7, kern.GetAdjustment(2, 1));
        Assert.Equal(0, kern.GetAdjustment(1, 1));
    }

    [Theory]
    [InlineData(0x0000)]
    [InlineData(0x0003)]
    [InlineData(0x0005)]
    [InlineData(0x0201)]
    public void SkipsSubtablesThatDoNotSetPairsApart(int coverage)
    {
        LegacyKerningTable kern = new LegacyKerningTable(SyntheticTables.KernWindows((coverage, [(1, 2, -30)])));

        Assert.False(kern.HasPairs);
        Assert.Equal(0, kern.GetAdjustment(1, 2));
    }

    [Fact]
    public void ReadsAppleKernTables()
    {
        LegacyKerningTable kern = new LegacyKerningTable(SyntheticTables.KernApple(
            (0x0000, [(1, 2, -25)]),
            (0x8000, [(1, 2, -99)]),
            (0x4000, [(1, 2, -99)]),
            (0x0002, [(1, 2, -99)])));

        Assert.Equal(-25, kern.GetAdjustment(1, 2));
    }

    [Fact]
    public void TrustsThePairCountOnlyAsFarAsTheTableGoes()
    {
        byte[] table = SyntheticTables.KernWindows((0x0001, [(1, 2, -30), (3, 4, -40)]));
        BigEndian.WriteUInt16(table, 4 + 6, 5000);

        LegacyKerningTable kern = new LegacyKerningTable(table);

        Assert.Equal(-40, kern.GetAdjustment(3, 4));
    }

    [Fact]
    public void StopsAtASubtableLengthTooShortToAdvance()
    {
        byte[] table = SyntheticTables.KernWindows((0x0001, [(1, 2, -30)]), (0x0001, [(1, 2, -30)]));
        BigEndian.WriteUInt16(table, 4 + 2, 0);

        Assert.Equal(-30, new LegacyKerningTable(table).GetAdjustment(1, 2));

        byte[] apple = SyntheticTables.KernApple((0x0000, [(1, 2, -30)]), (0x0000, [(1, 2, -30)]));
        BigEndian.WriteUInt32(apple, 8, 0);

        Assert.Equal(-30, new LegacyKerningTable(apple).GetAdjustment(1, 2));
    }

    [Fact]
    public void IgnoresSubtablesWithoutPairsAndCountsBeyondTheTable()
    {
        byte[] table = SyntheticTables.KernWindows((0x0001, []), (0x0001, [(1, 2, -12)]));

        // Claim five subtables where there are two; reading stops at the end of the table.
        BigEndian.WriteUInt16(table, 2, 5);
        LegacyKerningTable kern = new LegacyKerningTable(table);

        Assert.True(kern.HasPairs);
        Assert.Equal(-12, kern.GetAdjustment(1, 2));
    }

    [Fact]
    public void RejectsAnUnknownKernVersion()
    {
        Assert.Throws<FontFormatException>(() => new LegacyKerningTable(new byte[] { 0, 2, 0, 0, 0, 0 }));
    }

    [Fact]
    public void RejectsMoreKernSubtablesThanAFontCanUse()
    {
        (int, (int, int, int)[])[] subtables =
            Enumerable.Range(0, 4097).Select(_ => (0x0001, new[] { (1, 2, -1) })).ToArray();

        Assert.Throws<FontFormatException>(() => new LegacyKerningTable(SyntheticTables.KernWindows(subtables)));
    }

    // ---- GPOS ----------------------------------------------------------------------------------------------------

    [Fact]
    public void ReadsGlyphPairAdjustments()
    {
        GlyphPositioningKerning kern = Gpos((2, [Pairs(
            SyntheticLayout.CoverageFormat1(1, 3),
            [(2, [-40], []), (4, [-15], [])],
            [(1, [12], [])])]));

        Assert.True(kern.HasPairs);
        Assert.Equal(-40, kern.GetAdjustment(1, 2));
        Assert.Equal(-15, kern.GetAdjustment(1, 4));
        Assert.Equal(12, kern.GetAdjustment(3, 1));
        Assert.Equal(0, kern.GetAdjustment(1, 3));
        Assert.Equal(0, kern.GetAdjustment(2, 1));
    }

    [Fact]
    public void ConsultsTheNextSubtableWhenAGlyphPairSubtableLacksThePair()
    {
        GlyphPositioningKerning kern = Gpos((2,
        [
            Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-40], [])]),
            Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-99], []), (3, [-20], [])])
        ]));

        Assert.Equal(-40, kern.GetAdjustment(1, 2));
        Assert.Equal(-20, kern.GetAdjustment(1, 3));
    }

    [Fact]
    public void ReadsClassPairAdjustments()
    {
        // Glyphs 1-2 are first-class 1; glyph 5 is second-class 1 and glyphs 6-7 second-class 2.
        byte[] subtable = SyntheticLayout.PairFormat2(
            SyntheticLayout.CoverageFormat2((1, 3, 0)),
            SyntheticLayout.ClassFormat2((1, 2, 1)),
            SyntheticLayout.ClassFormat1(5, 1, 2, 2),
            SyntheticLayout.XAdvance,
            0,
            2,
            3,
            [0], [0], [0],
            [0], [-50], [-25]);

        GlyphPositioningKerning kern = Gpos((2, [subtable]));

        Assert.Equal(-50, kern.GetAdjustment(1, 5));
        Assert.Equal(-25, kern.GetAdjustment(2, 7));
        Assert.Equal(0, kern.GetAdjustment(3, 5));
        Assert.Equal(0, kern.GetAdjustment(1, 9));
        Assert.Equal(0, kern.GetAdjustment(4, 5));
    }

    [Fact]
    public void AClassSubtableAppliesEvenWithAZeroValue()
    {
        // The class subtable covers glyph 1 and gives it zero with glyph 2, so the glyph-pair subtable after it in
        // the same lookup must not be consulted.
        byte[] classes = SyntheticLayout.PairFormat2(
            SyntheticLayout.CoverageFormat1(1),
            SyntheticLayout.ClassFormat1(1, 0),
            SyntheticLayout.ClassFormat1(1, 0),
            SyntheticLayout.XAdvance,
            0,
            1,
            1,
            [0]);

        byte[] pairs = Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-99], [])]);
        GlyphPositioningKerning kern = Gpos((2, [classes, pairs]));

        Assert.Equal(0, kern.GetAdjustment(1, 2));
    }

    [Fact]
    public void IgnoresClassesBeyondTheDeclaredCounts()
    {
        byte[] subtable = SyntheticLayout.PairFormat2(
            SyntheticLayout.CoverageFormat1(1),
            SyntheticLayout.ClassFormat1(1, 3),
            SyntheticLayout.ClassFormat1(2, 4),
            SyntheticLayout.XAdvance,
            0,
            1,
            1,
            [-10]);

        GlyphPositioningKerning kern = Gpos((2, [subtable]));

        Assert.Equal(0, kern.GetAdjustment(1, 2));
    }

    [Fact]
    public void FindsTheAdvanceAfterOtherValueFields()
    {
        byte[] subtable = SyntheticLayout.PairFormat1(
            SyntheticLayout.CoverageFormat2((1, 1, 0)),
            SyntheticLayout.PlacementAndAdvance,
            SyntheticLayout.XAdvance,
            [(2, [100, -30], [55])]);

        Assert.Equal(-30, Gpos((2, [subtable])).GetAdjustment(1, 2));
    }

    [Fact]
    public void TreatsAValueRecordWithoutAnAdvanceAsZero()
    {
        byte[] subtable = SyntheticLayout.PairFormat1(
            SyntheticLayout.CoverageFormat1(1), 0x0001, 0, [(2, [100], [])]);

        Assert.Equal(0, Gpos((2, [subtable])).GetAdjustment(1, 2));
    }

    [Fact]
    public void AddsAdjustmentsFromSeveralLookups()
    {
        GlyphPositioningKerning kern = Gpos(
            (2, [Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-40], [])])]),
            (2, [Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-5], [])])]));

        Assert.Equal(-45, kern.GetAdjustment(1, 2));
    }

    [Fact]
    public void FollowsExtensionLookups()
    {
        GlyphPositioningKerning kern = Gpos((9, [
            SyntheticLayout.Extension(2, Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-40], [])])),
            SyntheticLayout.Extension(1, [0, 1, 0, 8, 0, 0])
        ]));

        Assert.Equal(-40, kern.GetAdjustment(1, 2));
    }

    [Fact]
    public void FollowsExtensionLookupsInACommittedFont()
    {
        OpenTypeFont font = TestFonts.Cff;

        Assert.IsType<GlyphPositioningKerning>(font.Kerning);
        Assert.Equal(-40, font.GetKerning(font.GetGlyphId('A'), font.GetGlyphId('V')));
        Assert.Equal(-70, font.GetKerning(font.GetGlyphId('T'), font.GetGlyphId('o')));
    }

    [Fact]
    public void RejectsAnExtensionPointingPastTheTable()
    {
        byte[] extension = new FontBytes().U16(1).U16(2).U32(0x7FFFFFF0).ToArray();

        Assert.Throws<FontFormatException>(() => Gpos((9, [extension])));
    }

    [Fact]
    public void SkipsLookupsOfOtherTypes()
    {
        GlyphPositioningKerning kern = Gpos((1, [new byte[] { 0, 1, 0, 6, 0, 4, 0, 1, 0, 1, 0, 1 }]));

        Assert.False(kern.HasPairs);
        Assert.Equal(0, kern.GetAdjustment(1, 2));
    }

    [Fact]
    public void UsesTheKernFeatureOfEveryScriptsDefaultLanguage()
    {
        byte[] first = Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-10], [])]);
        byte[] second = Pairs(SyntheticLayout.CoverageFormat1(3), [(4, [-20], [])]);
        byte[] ignored = Pairs(SyntheticLayout.CoverageFormat1(5), [(6, [-30], [])]);
        byte[] required = Pairs(SyntheticLayout.CoverageFormat1(7), [(8, [-40], [])]);

        byte[] gpos = SyntheticLayout.Gpos(
            [("DFLT", [0], -1), ("geor", [1, 9], -1), ("latn", null, -1), ("cyrl", [], 4)],
            [("kern", [0]), ("kern", [1]), ("liga", [2]), ("kern", [2]), ("kern", [3, 40])],
            [(2, [first]), (2, [second]), (2, [ignored]), (2, [required])]);

        GlyphPositioningKerning kern = new GlyphPositioningKerning(gpos);

        Assert.Equal(-10, kern.GetAdjustment(1, 2));
        Assert.Equal(-20, kern.GetAdjustment(3, 4));
        Assert.Equal(0, kern.GetAdjustment(5, 6));
        Assert.Equal(-40, kern.GetAdjustment(7, 8));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void HasNoKerningWhenAListIsMissing(int headerField)
    {
        byte[] gpos = SyntheticLayout.KernGpos((2, [Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-40], [])])]));
        BigEndian.WriteUInt16(gpos, headerField, 0);

        Assert.False(new GlyphPositioningKerning(gpos).HasPairs);
    }

    [Fact]
    public void FallsBackToTheKernTableWhenGposDoesNotKern()
    {
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("GPOS", SyntheticLayout.Gpos([("DFLT", [0], -1)], [("liga", [0])], [(2, [])]))
            .With("kern", SyntheticTables.KernWindows((0x0001, [(1, 2, -33)])))
            .Load();

        Assert.IsType<LegacyKerningTable>(font.Kerning);
        Assert.Equal(-33, font.GetKerning(1, 2));
    }

    [Fact]
    public void LeavesOutAKernTableThatCannotBeReadAsShapersDo()
    {
        byte[] kern = SyntheticTables.KernWindows((0x0001, [(1, 2, -33)]));

        OpenTypeFont font = SyntheticFont.Minimal().With("kern", kern.AsSpan(0, 7).ToArray()).Load();

        Assert.Null(font.Kerning);
        Assert.Equal(0, font.GetKerning(1, 2));
        Assert.Equal(1.3f, font.MeasureWidth("AB", 1f), 3);
    }

    [Fact]
    public void FallsBackToTheKernTableWhenGposCannotBeRead()
    {
        byte[] subtable = Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-40], [])]);
        BigEndian.WriteUInt16(subtable, 0, 3);

        OpenTypeFont font = SyntheticFont.Minimal()
            .With("GPOS", SyntheticLayout.KernGpos((2, [subtable])))
            .With("kern", SyntheticTables.KernWindows((0x0001, [(1, 2, -33)])))
            .Load();

        Assert.IsType<LegacyKerningTable>(font.Kerning);
        Assert.Equal(-33, font.GetKerning(1, 2));
    }

    [Fact]
    public void LeavesAPairWhosePairSetIsPastTheTableUnkerned()
    {
        // The first glyph's pair set is past the end of the table; the second's is intact.
        byte[] subtable = Pairs(SyntheticLayout.CoverageFormat1(1, 2), [(2, [-40], [])], [(1, [12], [])]);
        BigEndian.WriteUInt16(subtable, 10, 0xFFF0);

        OpenTypeFont font = SyntheticFont.Minimal()
            .With("GPOS", SyntheticLayout.KernGpos((2, [subtable])))
            .Load();

        Assert.Equal(0, font.GetKerning(1, 2));
        Assert.Equal(12, font.GetKerning(2, 1));
        Assert.Equal(1.3f, font.MeasureWidth("AB", 1f), 3);
    }

    [Fact]
    public void HasNoKerningWhenNeitherTableKerns()
    {
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("kern", SyntheticTables.KernWindows((0x0000, [(1, 2, -33)])))
            .Load();

        Assert.Null(font.Kerning);
        Assert.Equal(0, font.GetKerning(1, 2));
        Assert.Equal(0f, font.GetKerning(1, 2, 12f));
    }

    [Fact]
    public void RejectsKerningGlyphsOutsideTheFont()
    {
        OpenTypeFont font = SyntheticFont.Minimal().Load();

        Assert.Throws<ArgumentOutOfRangeException>(() => font.GetKerning(3, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => font.GetKerning(1, 3));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public void RejectsCoverageAndClassFormatsThatDoNotExist(int format)
    {
        byte[] coverage = new FontBytes().U16(format).U16(0).ToArray();

        Assert.Throws<FontFormatException>(() =>
            Gpos((2, [SyntheticLayout.PairFormat1(coverage, SyntheticLayout.XAdvance, 0)])));
        Assert.Throws<FontFormatException>(() => new ClassDefinition(coverage, 0));
    }

    [Fact]
    public void RejectsAPairAdjustmentFormatThatDoesNotExist()
    {
        byte[] subtable = SyntheticLayout.PairFormat1(SyntheticLayout.CoverageFormat1(1), SyntheticLayout.XAdvance, 0);
        BigEndian.WriteUInt16(subtable, 0, 3);

        Assert.Throws<FontFormatException>(() => Gpos((2, [subtable])));
    }

    [Fact]
    public void RejectsAClassMatrixLargerThanTheTable()
    {
        byte[] subtable = SyntheticLayout.PairFormat2(
            SyntheticLayout.CoverageFormat1(1),
            SyntheticLayout.ClassFormat1(1, 0),
            SyntheticLayout.ClassFormat1(1, 0),
            SyntheticLayout.XAdvance,
            0,
            200,
            200);

        Assert.Throws<FontFormatException>(() => Gpos((2, [subtable])));
    }

    [Fact]
    public void ReadsASubtableSharedByManyReferencesOnce()
    {
        byte[] subtable = Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-1], [])]);
        byte[] gpos = SyntheticLayout.GposOfLookups(
            [("DFLT", [0], -1)], [("kern", [0])], [SyntheticLayout.SharedLookup(2, 4096, subtable)]);

        // Within a lookup the first applicable subtable wins, however many copies follow it.
        Assert.Equal(-1, new GlyphPositioningKerning(gpos).GetAdjustment(1, 2));
    }

    [Fact]
    public void RejectsMoreKerningSubtablesThanAFontCanUse()
    {
        byte[] subtable = Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-1], [])]);
        byte[] gpos = SyntheticLayout.GposOfLookups(
            [("DFLT", [0], -1)], [("kern", [0])], [SyntheticLayout.SharedLookup(2, 4097, subtable)]);

        Assert.Throws<FontFormatException>(() => new GlyphPositioningKerning(gpos));
    }

    [Fact]
    public void RejectsListsDeclaringMoreEntriesThanAFontCanUse()
    {
        // One language system listing 65 535 features, shared by every script.
        const int Scripts = 8;
        FontBytes gpos = new FontBytes().U16(1).U16(0).U16(10).U16(1).U16(1).U16(Scripts);

        for (int script = 0; script < Scripts; script++)
            gpos.Tag("latn").U16(2 + (6 * Scripts));

        gpos.U16(4).U16(0).U16(0).U16(0xFFFF).U16(0xFFFF).Zeros(2 * 0xFFFF);

        Assert.Throws<FontFormatException>(() => new GlyphPositioningKerning(gpos.ToArray()));
    }

    [Fact]
    public void KernsWhatAPairSetHoldsWhenItDeclaresMore()
    {
        byte[] subtable = Pairs(SyntheticLayout.CoverageFormat1(1), [(2, [-40], []), (4, [-15], [])]);
        BigEndian.WriteUInt16(subtable, BigEndian.UInt16(subtable, 10), 5000);

        GlyphPositioningKerning kern = Gpos((2, [subtable]));

        Assert.Equal(-40, kern.GetAdjustment(1, 2));
        Assert.Equal(-15, kern.GetAdjustment(1, 4));
        Assert.Equal(0, kern.GetAdjustment(1, 9));
    }

    [Fact]
    public void ReadsNothingFromAnEmptyPairSet()
    {
        GlyphPositioningKerning kern = Gpos((2, [Pairs(SyntheticLayout.CoverageFormat1(1), [])]));

        Assert.Equal(0, kern.GetAdjustment(1, 2));
    }
}
