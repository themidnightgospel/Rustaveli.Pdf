using Rustaveli.Pdf.Fonts;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SyntheticSubstitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

/// <summary>Lookup type 6 in its three formats: context before and after the input.</summary>
public class ChainedContextSubstitutionTests
{
    private static byte[] Cover(params int[] glyphs) => SyntheticLayout.CoverageFormat1(glyphs);

    /// <summary>Lookup 1 of every table here: adds 100 to glyphs 1 to 99.</summary>
    private static byte[] PlusHundred() => Lookup(1, SingleFormat1(SyntheticLayout.CoverageFormat2((1, 99, 0)), 100));

    private static byte[] Table(byte[] chain, int flags = 0) =>
        SyntheticSubstitution.Gsub([Lookup(6, flags, 0, chain), PlusHundred()]);

    // ---- Format 1 ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, new[] { 1, 102, 3 })]
    [InlineData(new[] { 2, 3 }, new[] { 2, 3 })]
    [InlineData(new[] { 1, 2 }, new[] { 1, 2 })]
    [InlineData(new[] { 5, 2, 3 }, new[] { 5, 2, 3 })]
    [InlineData(new[] { 1, 2, 4 }, new[] { 1, 2, 4 })]
    public void MatchesTheGlyphsBeforeAndAfterTheInput(int[] glyphs, int[] expected)
    {
        byte[] gsub = Table(ChainFormat1(Cover(2), [ChainRule([1], [], [3], (0, 1))]));

        Assert.Equal(expected, Glyphs(Apply(gsub, glyphs)));
    }

    [Theory]
    [InlineData(new[] { 6, 5, 2, 3, 7, 8 }, new[] { 6, 5, 2, 103, 7, 8 })]
    [InlineData(new[] { 5, 6, 2, 3, 7, 8 }, new[] { 5, 6, 2, 3, 7, 8 })]
    [InlineData(new[] { 6, 5, 2, 3, 8, 7 }, new[] { 6, 5, 2, 3, 8, 7 })]
    [InlineData(new[] { 6, 5, 2, 4, 7, 8 }, new[] { 6, 5, 2, 4, 7, 8 })]
    [InlineData(new[] { 5, 2, 3, 7, 8 }, new[] { 5, 2, 3, 7, 8 })]
    [InlineData(new[] { 6, 5, 2, 3, 7 }, new[] { 6, 5, 2, 3, 7 })]
    public void MatchesTheBacktrackNearestGlyphFirst(int[] glyphs, int[] expected)
    {
        // Backtrack 5 then 6 reads, in text order, 6 5; the input is 2 3 and the lookahead 7 8.
        byte[] gsub = Table(ChainFormat1(Cover(2), [ChainRule([5, 6], [3], [7, 8], (1, 1))]));

        Assert.Equal(expected, Glyphs(Apply(gsub, glyphs)));
    }

    [Fact]
    public void ContinuesAfterTheInputNotTheLookahead()
    {
        byte[] gsub = Table(ChainFormat1(Cover(2), [ChainRule([], [], [2], (0, 1))]));

        Assert.Equal(new[] { 102, 102, 2 }, Glyphs(Apply(gsub, [2, 2, 2])));
    }

    [Fact]
    public void MatchesNothingWithAChainedRuleOfNoInput()
    {
        byte[] gsub = Table(ChainFormat1(Cover(2), [ChainRule([], 0, [], [], (0, 1)), ChainRule([], [], [], (0, 1))]));

        Assert.Equal(new[] { 102 }, Glyphs(Apply(gsub, [2])));
    }

    [Fact]
    public void IgnoresAGlyphWithoutChainedRules()
    {
        byte[] gsub = Table(ChainFormat1(Cover(2, 3), null, [ChainRule([], [], [], (0, 1))]));

        Assert.Equal(new[] { 2, 103 }, Glyphs(Apply(gsub, [2, 3])));
    }

    [Fact]
    public void MatchesPastTheGlyphsTheLookupPassesOver()
    {
        // Glyph 4 is a mark; the lookup passes over marks, before the input and after it alike.
        byte[] gsub = Table(ChainFormat1(Cover(2), [ChainRule([1], [3], [5], (1, 1))]), flags: 0x0008);
        byte[] gdef = SyntheticLayout.Gdef(SyntheticLayout.ClassFormat1(1, 1, 1, 1, 3, 1));

        Assert.Equal(new[] { 1, 4, 2, 4, 103, 4, 5 }, Glyphs(Apply(gsub, [1, 4, 2, 4, 3, 4, 5], gdef)));
        Assert.Equal(new[] { 1, 4, 2, 4, 3, 4, 5 }, Glyphs(Apply(Table(ChainFormat1(
            Cover(2), [ChainRule([1], [3], [5], (1, 1))])), [1, 4, 2, 4, 3, 4, 5], gdef)));
    }

    // ---- Format 2 ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(new[] { 5, 2, 3, 6 }, new[] { 5, 102, 3, 6 })]
    [InlineData(new[] { 6, 2, 3, 6 }, new[] { 6, 2, 3, 6 })]
    [InlineData(new[] { 5, 2, 3, 5 }, new[] { 5, 2, 3, 5 })]
    [InlineData(new[] { 5, 2, 2, 6 }, new[] { 5, 2, 2, 6 })]
    public void MatchesEachPartAgainstItsOwnClasses(int[] glyphs, int[] expected)
    {
        // Backtrack class 1 is glyph 5, lookahead class 1 is glyph 6, and input classes 1 and 2 are glyphs 2 and 3:
        // each glyph is class 0 in the other parts' definitions.
        byte[] gsub = Table(ChainFormat2(
            Cover(2),
            SyntheticLayout.ClassFormat1(5, 1),
            SyntheticLayout.ClassFormat1(2, 1, 2),
            SyntheticLayout.ClassFormat1(6, 1),
            null,
            [ChainRule([1], [2], [1], (0, 1))]));

        Assert.Equal(expected, Glyphs(Apply(gsub, glyphs)));
    }

    [Fact]
    public void PutsEveryGlyphInClassZeroOfAPartWithoutClasses()
    {
        byte[] gsub = Table(ChainFormat2(Cover(2), null, null, null, [ChainRule([0], [0], [0], (1, 1))]));

        Assert.Equal(new[] { 7, 2, 108, 9 }, Glyphs(Apply(gsub, [7, 2, 8, 9])));
    }

    [Fact]
    public void MatchesOnlyCoveredGlyphsOfAChainedClassRule()
    {
        byte[] gsub = Table(ChainFormat2(Cover(2), null, null, null, [ChainRule([], [], [], (0, 1))]));

        Assert.Equal(new[] { 1, 102 }, Glyphs(Apply(gsub, [1, 2])));
    }

    [Fact]
    public void IgnoresAnInputClassPastTheChainedRuleSets()
    {
        byte[] gsub = Table(ChainFormat2(
            Cover(2, 3), null, SyntheticLayout.ClassFormat1(3, 1), null, [ChainRule([], [], [], (0, 1))]));

        Assert.Equal(new[] { 102, 3 }, Glyphs(Apply(gsub, [2, 3])));
    }

    // ---- Format 3 ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(new[] { 6, 5, 2, 3, 7, 8 }, new[] { 6, 5, 2, 103, 7, 8 })]
    [InlineData(new[] { 5, 6, 2, 3, 7, 8 }, new[] { 5, 6, 2, 3, 7, 8 })]
    [InlineData(new[] { 6, 5, 2, 3, 8, 7 }, new[] { 6, 5, 2, 3, 8, 7 })]
    [InlineData(new[] { 6, 5, 2, 9, 7, 8 }, new[] { 6, 5, 2, 9, 7, 8 })]
    [InlineData(new[] { 6, 5, 9, 3, 7, 8 }, new[] { 6, 5, 9, 3, 7, 8 })]
    public void MatchesACoverageTablePerContextGlyph(int[] glyphs, int[] expected)
    {
        byte[] gsub = Table(ChainFormat3(
            [Cover(5), Cover(6)], [Cover(2), Cover(3)], [Cover(7), Cover(8)], (1, 1)));

        Assert.Equal(expected, Glyphs(Apply(gsub, glyphs)));
    }

    [Fact]
    public void MatchesNothingWithoutChainedInputCoverage()
    {
        Assert.Equal(new[] { 2 }, Glyphs(Apply(Table(ChainFormat3([], [], [], (0, 1))), [2])));
    }

    [Fact]
    public void RejectsChainedRuleSetsPastTheTable()
    {
        byte[] subtable = ChainFormat2(Cover(2), null, null, null, [ChainRule([], [], [], (0, 1))]);
        BigEndian.WriteUInt16(subtable, 10, 5000);

        Assert.Throws<FontFormatException>(() => ReadSubtable(6, subtable));
    }
}
