using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SyntheticSubstitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

/// <summary>Lookup type 5 in its three formats, and how the lookups a rule calls apply.</summary>
public class ContextSubstitutionTests
{
    /// <summary>An empty sequence, which deletes the glyph.</summary>
    private static readonly int[] NoGlyphs = [];

    private static byte[] Cover(params int[] glyphs) => SyntheticLayout.CoverageFormat1(glyphs);

    /// <summary>Lookup 1 of every table here: adds 100 to glyphs 1 to 99.</summary>
    private static byte[] PlusHundred() => Lookup(1, SingleFormat1(SyntheticLayout.CoverageFormat2((1, 99, 0)), 100));

    private static byte[] Table(byte[] context, params byte[][] more) =>
        SyntheticSubstitution.Gsub([Lookup(5, context), PlusHundred(), .. more]);

    // ---- Format 1 ------------------------------------------------------------------------------------------------

    [Fact]
    public void AppliesTheRuleLookupsAtTheirSequenceIndex()
    {
        byte[] gsub = Table(ContextFormat1(Cover(1), [Rule([2, 3], (1, 1))]));

        Assert.Equal(new[] { 1, 102, 3, 4 }, Glyphs(Apply(gsub, [1, 2, 3, 4])));
        Assert.Equal(new[] { 1, 2, 4 }, Glyphs(Apply(gsub, [1, 2, 4])));
    }

    [Fact]
    public void ContinuesAfterTheInput()
    {
        byte[] gsub = Table(ContextFormat1(Cover(1), [Rule([1], (0, 1))]));

        Assert.Equal(new[] { 101, 1, 101, 1 }, Glyphs(Apply(gsub, [1, 1, 1, 1])));
    }

    [Fact]
    public void TriesRulesInOrder()
    {
        byte[] gsub = Table(ContextFormat1(Cover(1), [Rule([3], (0, 1)), Rule([2], (1, 1))]));

        Assert.Equal(new[] { 1, 102 }, Glyphs(Apply(gsub, [1, 2])));
        Assert.Equal(new[] { 101, 3 }, Glyphs(Apply(gsub, [1, 3])));
    }

    [Fact]
    public void AppliesARuleOfOneGlyph()
    {
        Assert.Equal(new[] { 101, 2 }, Glyphs(Apply(Table(ContextFormat1(Cover(1), [Rule([], (0, 1))])), [1, 2])));
    }

    [Fact]
    public void IgnoresAGlyphWithoutRules()
    {
        byte[] nullSet = Table(ContextFormat1(Cover(1, 2), [Rule([], (0, 1))], null));
        byte[] missingSet = Table(ContextFormat1(Cover(1, 2), [Rule([], (0, 1))]));

        Assert.Equal(new[] { 101, 2 }, Glyphs(Apply(nullSet, [1, 2])));
        Assert.Equal(new[] { 101, 2 }, Glyphs(Apply(missingSet, [1, 2])));
    }

    [Fact]
    public void MatchesNothingWithARuleOfNoGlyphs()
    {
        byte[] gsub = Table(ContextFormat1(Cover(1), [Rule(0, [], (0, 1)), Rule([2], (1, 1))]));

        Assert.Equal(new[] { 1, 102 }, Glyphs(Apply(gsub, [1, 2])));
    }

    [Fact]
    public void SkipsLookupRecordsThatPointNowhere()
    {
        byte[] gsub = Table(ContextFormat1(Cover(1), [Rule([2], (2, 1), (0, 9), (1, 1))]));

        Assert.Equal(new[] { 1, 102 }, Glyphs(Apply(gsub, [1, 2])));
    }

    [Fact]
    public void AppliesLookupRecordsInTheirOrder()
    {
        byte[] oneToFive = Lookup(1, SingleFormat2(Cover(1), 5));
        byte[] fiveToSix = Lookup(1, SingleFormat2(Cover(5), 6));
        byte[] forward = Table(ContextFormat1(Cover(1), [Rule([], (0, 2), (0, 3))]), oneToFive, fiveToSix);
        byte[] backward = Table(ContextFormat1(Cover(1), [Rule([], (0, 3), (0, 2))]), oneToFive, fiveToSix);

        Assert.Equal(new[] { 6 }, Glyphs(Apply(forward, [1])));
        Assert.Equal(new[] { 5 }, Glyphs(Apply(backward, [1])));
    }

    // ---- Nested lookups that change the sequence -----------------------------------------------------------------

    [Fact]
    public void CountsGlyphsAMultipleSubstitutionInsertedInLaterSequenceIndices()
    {
        // Glyph 1 becomes 7 and 8, so the 2 that was input glyph 1 is now input glyph 2.
        byte[] gsub = Table(
            ContextFormat1(Cover(1), [Rule([2], (0, 2), (2, 1))]), Lookup(2, Multiple(Cover(1), [7, 8])));

        GlyphBuffer buffer = Apply(gsub, [1, 2, 3]);

        Assert.Equal(new[] { 7, 8, 102, 3 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 0, 1, 2 }, Clusters(buffer));
    }

    [Fact]
    public void KeepsTheSequenceWhenAMultipleSubstitutionMakesOneGlyph()
    {
        byte[] gsub = Table(
            ContextFormat1(Cover(1), [Rule([2], (0, 2), (1, 1))]), Lookup(2, Multiple(Cover(1), [9])));

        Assert.Equal(new[] { 9, 102 }, Glyphs(Apply(gsub, [1, 2])));
    }

    [Fact]
    public void CountsTheGlyphsALigatureLeftInLaterSequenceIndices()
    {
        // Glyphs 1 and 2 become 10, so the 3 that was input glyph 2 is now input glyph 1.
        byte[] gsub = Table(
            ContextFormat1(Cover(1), [Rule([2, 3], (0, 2), (1, 1))]), Lookup(4, Ligature(Cover(1), [(10, [2])])));

        GlyphBuffer buffer = Apply(gsub, [1, 2, 3, 4]);

        Assert.Equal(new[] { 10, 103, 4 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 2, 3 }, Clusters(buffer));
    }

    [Fact]
    public void DropsAnInputGlyphANestedLookupDeleted()
    {
        byte[] gsub = Table(
            ContextFormat1(Cover(1), [Rule([2], (0, 2), (0, 1))]), Lookup(2, Multiple(Cover(1), NoGlyphs)));

        GlyphBuffer buffer = Apply(gsub, [1, 2, 1, 3]);

        Assert.Equal(new[] { 102, 1, 3 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 2, 3 }, Clusters(buffer));
    }

    [Fact]
    public void GoesOnWhereTheInputBeganOnceEveryInputGlyphIsDeleted()
    {
        byte[] gsub = Table(ContextFormat1(Cover(1), [Rule([], (0, 2))]), Lookup(2, Multiple(Cover(1), NoGlyphs)));

        Assert.Equal(new[] { 3 }, Glyphs(Apply(gsub, [1, 1, 3])));
    }

    [Fact]
    public void LeavesOutOfTheSequenceAGlyphItPassedOverThatALigatureRemoved()
    {
        // The context passes over the mark 4; the nested ligature, which does not, takes it as a component.
        byte[] gsub = SyntheticSubstitution.Gsub(
        [
            Lookup(5, 0x0008, 0, ContextFormat1(Cover(1), [Rule([2], (0, 2), (1, 1))])),
            PlusHundred(),
            Lookup(4, Ligature(Cover(1), [(10, [4])]))
        ]);

        GlyphBuffer buffer = Apply(gsub, [1, 4, 2], SyntheticLayout.Gdef(SyntheticLayout.ClassFormat1(1, 1, 1, 1, 3)));

        Assert.Equal(new[] { 10, 102 }, Glyphs(buffer));
    }

    [Fact]
    public void LeavesOutOfTheSequenceGlyphsInsertedAfterAGlyphItPassedOver()
    {
        // The context passes over the mark 4. Its first lookup is a context of its own that sees the mark and has it
        // doubled; the outer rule's second glyph, 2, must still be found for its second lookup.
        byte[] gsub = SyntheticSubstitution.Gsub(
        [
            Lookup(5, 0x0008, 0, ContextFormat1(Cover(1), [Rule([2], (0, 2), (1, 1))])),
            PlusHundred(),
            Lookup(5, ContextFormat1(Cover(1), [Rule([4], (1, 3))])),
            Lookup(2, Multiple(Cover(4), [4, 4]))
        ]);

        GlyphBuffer buffer = Apply(gsub, [1, 4, 2], SyntheticLayout.Gdef(SyntheticLayout.ClassFormat1(1, 1, 1, 1, 3)));

        Assert.Equal(new[] { 1, 4, 4, 102 }, Glyphs(buffer));
    }

    [Fact]
    public void StopsNestingLookupsAtTheLimit()
    {
        // The rule adds one and calls itself again, at every depth until nesting stops.
        byte[] gsub = SyntheticSubstitution.Gsub(
        [
            Lookup(5, ContextFormat3([SyntheticLayout.CoverageFormat2((1, 900, 0))], (0, 1), (0, 0))),
            Lookup(1, SingleFormat1(SyntheticLayout.CoverageFormat2((1, 900, 0)), 1))
        ]);

        Assert.Equal(new[] { 1 + SubstitutionSession.MaximumNesting }, Glyphs(Apply(gsub, [1])));
    }

    [Theory]
    [InlineData(1, new[] { 1, 2 })]
    [InlineData(2, new[] { 1, 2 })]
    [InlineData(3, new[] { 101, 2 })]
    public void StopsTryingRulesOnceTheWorkIsSpent(int work, int[] expected)
    {
        // One unit to try the subtable, one for its rule, and one for the subtable of the lookup the rule calls.
        byte[] gsub = Table(ContextFormat1(Cover(1), [Rule([], (0, 1))]));

        Assert.Equal(expected, Glyphs(ApplyWithLimits(gsub, [1, 2], work, 100)));
    }

    // ---- Format 2 ------------------------------------------------------------------------------------------------

    [Fact]
    public void MatchesRulesByClass()
    {
        // Glyph 1 is class 1; glyphs 2 and 3 are class 2. Class 1 has rules; class 0 has none.
        byte[] gsub = Table(ContextFormat2(
            Cover(1, 2, 3, 5), SyntheticLayout.ClassFormat2((1, 1, 1), (2, 3, 2)), null, [Rule([2], (1, 1))]));

        Assert.Equal(new[] { 1, 103 }, Glyphs(Apply(gsub, [1, 3])));
        Assert.Equal(new[] { 1, 102 }, Glyphs(Apply(gsub, [1, 2])));
        Assert.Equal(new[] { 1, 1 }, Glyphs(Apply(gsub, [1, 1])));
        Assert.Equal(new[] { 5, 3 }, Glyphs(Apply(gsub, [5, 3])));
    }

    [Fact]
    public void MatchesOnlyCoveredGlyphsWhateverTheirClass()
    {
        byte[] gsub = Table(ContextFormat2(Cover(2), null, [Rule([], (0, 1))]));

        Assert.Equal(new[] { 1, 102 }, Glyphs(Apply(gsub, [1, 2])));
    }

    [Fact]
    public void PutsEveryGlyphInClassZeroWithoutAClassDefinition()
    {
        byte[] gsub = Table(ContextFormat2(Cover(1), null, [Rule([0], (0, 1))]));

        Assert.Equal(new[] { 101, 7 }, Glyphs(Apply(gsub, [1, 7])));
    }

    [Fact]
    public void IgnoresAClassPastTheRuleSets()
    {
        byte[] gsub = Table(ContextFormat2(Cover(1, 2), SyntheticLayout.ClassFormat1(1, 0, 3), [Rule([], (0, 1))]));

        Assert.Equal(new[] { 101, 2 }, Glyphs(Apply(gsub, [1, 2])));
    }

    // ---- Format 3 ------------------------------------------------------------------------------------------------

    [Fact]
    public void MatchesACoverageTablePerGlyph()
    {
        byte[] gsub = Table(ContextFormat3([Cover(1, 2), Cover(3, 4)], (1, 1)));

        Assert.Equal(new[] { 2, 104 }, Glyphs(Apply(gsub, [2, 4])));
        Assert.Equal(new[] { 2, 5 }, Glyphs(Apply(gsub, [2, 5])));
        Assert.Equal(new[] { 3, 4 }, Glyphs(Apply(gsub, [3, 4])));
    }

    [Fact]
    public void MatchesNothingWithoutInputCoverage()
    {
        Assert.Equal(new[] { 1 }, Glyphs(Apply(Table(ContextFormat3([], (0, 1))), [1])));
    }

    // ---- Malformed -----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RejectsRuleSetsPastTheTable(int format)
    {
        byte[] subtable = format == 1
            ? ContextFormat1(Cover(1), [Rule([], (0, 1))])
            : ContextFormat2(Cover(1), null, [Rule([], (0, 1))]);

        BigEndian.WriteUInt16(subtable, format == 1 ? 4 : 6, 5000);

        Assert.Throws<FontFormatException>(() => Apply(Table(subtable), [1]));
    }

    [Fact]
    public void RejectsARuleSetOffsetPastTheTable()
    {
        byte[] subtable = ContextFormat1(Cover(1), [Rule([], (0, 1))]);
        BigEndian.WriteUInt16(subtable, 6, 0xFFF0);

        Assert.Throws<FontFormatException>(() => Apply(Table(subtable), [1]));
    }
}
