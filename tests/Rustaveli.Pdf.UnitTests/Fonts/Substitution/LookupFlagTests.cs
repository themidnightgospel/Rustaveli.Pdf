using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SyntheticSubstitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

/// <summary>Which glyphs a lookup passes over, as its flags and the font's GDEF table decide.</summary>
public class LookupFlagTests
{
    private const int IgnoreBaseGlyphs = 0x0002;
    private const int IgnoreLigatures = 0x0004;
    private const int IgnoreMarks = 0x0008;
    private const int UseMarkFilteringSet = 0x0010;

    // Glyph 1 is a base, 2 a ligature, 3 and 4 marks of attachment classes 1 and 2, 5 a base; mark set 0 is glyph 3.
    private const int Base = 1;
    private const int Ligature = 2;
    private const int Mark = 3;
    private const int OtherMark = 4;
    private const int Final = 5;

    private static readonly byte[] Definitions = SyntheticLayout.Gdef(
        SyntheticLayout.ClassFormat1(1, 1, 2, 3, 3, 1),
        SyntheticLayout.ClassFormat1(3, 1, 2),
        [SyntheticLayout.CoverageFormat1(Mark)]);

    /// <summary>A lookup adding 100 to glyphs 1 to 5.</summary>
    private static byte[] Single(int flags, int markSet = 0) => SyntheticSubstitution.Gsub(
        [Lookup(1, flags, markSet, SingleFormat1(SyntheticLayout.CoverageFormat2((1, 5, 0)), 100))]);

    /// <summary>A lookup adding 100 to the base glyph 1 when the glyph 5 follows it.</summary>
    private static byte[] BeforeFinal(int flags, int markSet = 0) => SyntheticSubstitution.Gsub(
    [
        Lookup(6, flags, markSet, ChainFormat3(
            [], [SyntheticLayout.CoverageFormat1(Base)], [SyntheticLayout.CoverageFormat1(Final)], (0, 1))),
        Lookup(1, SingleFormat1(SyntheticLayout.CoverageFormat1(Base), 100))
    ]);

    [Theory]
    [InlineData(0, new[] { 101, 102, 103, 104, 105 })]
    [InlineData(IgnoreBaseGlyphs, new[] { 1, 102, 103, 104, 5 })]
    [InlineData(IgnoreLigatures, new[] { 101, 2, 103, 104, 105 })]
    [InlineData(IgnoreMarks, new[] { 101, 102, 3, 4, 105 })]
    [InlineData(0x0100, new[] { 101, 102, 103, 4, 105 })]
    [InlineData(0x0200, new[] { 101, 102, 3, 104, 105 })]
    public void DoesNotSubstituteAGlyphItPassesOver(int flags, int[] expected)
    {
        Assert.Equal(expected, Glyphs(Apply(Single(flags), [1, 2, 3, 4, 5], Definitions)));
    }

    [Theory]
    [InlineData(0, new[] { 101, 102, 103, 4, 105 })]
    [InlineData(7, new[] { 101, 102, 3, 4, 105 })]
    public void FiltersMarksBySet(int markSet, int[] expected)
    {
        Assert.Equal(expected, Glyphs(Apply(Single(UseMarkFilteringSet, markSet), [1, 2, 3, 4, 5], Definitions)));
    }

    [Theory]
    [InlineData(0, new[] { 1, 3, 5 }, new[] { 1, 3, 5 })]
    [InlineData(IgnoreMarks, new[] { 1, 3, 4, 5 }, new[] { 101, 3, 4, 5 })]
    [InlineData(IgnoreLigatures, new[] { 1, 2, 2, 5 }, new[] { 101, 2, 2, 5 })]
    [InlineData(IgnoreMarks, new[] { 1, 2, 5 }, new[] { 1, 2, 5 })]
    [InlineData(0x0200, new[] { 1, 3, 5 }, new[] { 101, 3, 5 })]
    [InlineData(0x0200, new[] { 1, 4, 5 }, new[] { 1, 4, 5 })]
    public void MatchesContextPastTheGlyphsItPassesOver(int flags, int[] glyphs, int[] expected)
    {
        Assert.Equal(expected, Glyphs(Apply(BeforeFinal(flags), glyphs, Definitions)));
    }

    [Theory]
    [InlineData(0, new[] { 1, 4, 5 }, new[] { 101, 4, 5 })]
    [InlineData(0, new[] { 1, 3, 5 }, new[] { 1, 3, 5 })]
    [InlineData(7, new[] { 1, 3, 5 }, new[] { 101, 3, 5 })]
    public void MatchesContextPastMarksOutsideTheSet(int markSet, int[] glyphs, int[] expected)
    {
        // Mark set 0 holds glyph 3 alone, so glyph 4 is passed over and glyph 3 is not; set 7 does not exist and
        // holds no marks at all.
        byte[] gsub = BeforeFinal(UseMarkFilteringSet, markSet);

        Assert.Equal(expected, Glyphs(Apply(gsub, glyphs, Definitions)));
    }

    [Fact]
    public void PassesOverBaseGlyphsInContext()
    {
        // With base glyphs passed over, the chained lookup cannot start at the base 1 at all.
        Assert.Equal(new[] { 1, 5 }, Glyphs(Apply(BeforeFinal(IgnoreBaseGlyphs), [1, 5], Definitions)));
    }

    [Fact]
    public void PassesOverNothingWithoutGlyphClasses()
    {
        byte[] ignoring = Single(IgnoreMarks | IgnoreBaseGlyphs);
        byte[] filtering = Single(UseMarkFilteringSet);

        Assert.Equal(new[] { 101, 102, 103, 104, 105 }, Glyphs(Apply(ignoring, [1, 2, 3, 4, 5])));
        Assert.Equal(new[] { 101, 102, 103, 104, 105 }, Glyphs(Apply(filtering, [1, 2, 3, 4, 5])));
    }

    [Fact]
    public void MatchesBacktrackPastTheGlyphsItPassesOver()
    {
        Assert.Equal(new[] { 1, 3, 4, 105 }, Glyphs(Apply(AfterBase(), [1, 3, 4, 5], Definitions)));
        Assert.Equal(new[] { 3, 5 }, Glyphs(Apply(AfterBase(), [3, 5], Definitions)));
    }

    [Theory]
    [InlineData(4, new[] { 1, 3, 3, 5 })]
    [InlineData(5, new[] { 1, 3, 3, 5 })]
    [InlineData(6, new[] { 101, 3, 3, 5 })]
    public void StopsLookingAheadOnceTheWorkIsSpent(int work, int[] expected)
    {
        // One unit for the subtable, one for its rule, one per glyph stepped over or onto, and one for the subtable
        // of the lookup the rule calls.
        byte[] gsub = BeforeFinal(IgnoreMarks);

        Assert.Equal(expected, Glyphs(ApplyWithLimits(gsub, [1, 3, 3, 5], work, 100, Definitions)));
    }

    [Theory]
    [InlineData(5, new[] { 1, 3, 4, 5 })]
    [InlineData(6, new[] { 1, 3, 4, 5 })]
    [InlineData(7, new[] { 1, 3, 4, 105 })]
    public void StopsLookingBackOnceTheWorkIsSpent(int work, int[] expected)
    {
        // As looking ahead, plus the subtable tried at the first glyph, which it does not cover.
        Assert.Equal(expected, Glyphs(ApplyWithLimits(AfterBase(), [1, 3, 4, 5], work, 100, Definitions)));
    }

    /// <summary>A lookup adding 100 to the glyph 5 when the base glyph 1 comes before it, passing over marks.</summary>
    private static byte[] AfterBase() => SyntheticSubstitution.Gsub(
    [
        Lookup(6, IgnoreMarks, 0, ChainFormat3(
            [SyntheticLayout.CoverageFormat1(Base)], [SyntheticLayout.CoverageFormat1(Final)], [], (0, 1))),
        Lookup(1, SingleFormat1(SyntheticLayout.CoverageFormat1(Final), 100))
    ]);
}
