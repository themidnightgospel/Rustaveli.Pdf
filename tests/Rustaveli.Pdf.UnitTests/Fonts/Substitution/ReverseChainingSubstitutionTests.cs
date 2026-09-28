using Rustaveli.Pdf.Fonts;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SyntheticSubstitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

public class ReverseChainingSubstitutionTests
{
    private static byte[] Cover(params int[] glyphs) => SyntheticLayout.CoverageFormat1(glyphs);

    [Fact]
    public void SubstitutesFromTheEndBackSoEachChoiceSeesTheNext()
    {
        // 1 becomes 2 before a 2: from the end back, each new 2 turns the 1 before it too.
        byte[] gsub = OneLookup(8, ReverseChain(Cover(1), [], [Cover(2)], 2));

        Assert.Equal(new[] { 2, 2, 2, 3 }, Glyphs(Apply(gsub, [1, 1, 2, 3])));
        Assert.Equal(new[] { 1, 1, 3 }, Glyphs(Apply(gsub, [1, 1, 3])));
    }

    [Fact]
    public void MatchesTheBacktrackNearestGlyphFirst()
    {
        byte[] gsub = OneLookup(8, ReverseChain(Cover(1), [Cover(5), Cover(6)], [], 7));

        Assert.Equal(new[] { 6, 5, 7 }, Glyphs(Apply(gsub, [6, 5, 1])));
        Assert.Equal(new[] { 5, 6, 1 }, Glyphs(Apply(gsub, [5, 6, 1])));
        Assert.Equal(new[] { 5, 1 }, Glyphs(Apply(gsub, [5, 1])));
    }

    [Fact]
    public void IgnoresCoverageIndicesPastTheSubstitutes()
    {
        byte[] gsub = OneLookup(8, ReverseChain(Cover(1, 2), [], [], 7));

        Assert.Equal(new[] { 7, 2 }, Glyphs(Apply(gsub, [1, 2])));
    }

    [Fact]
    public void RefusesASubstituteTheFontLacks()
    {
        byte[] gsub = OneLookup(8, ReverseChain(Cover(1), [], [], 1000));

        Assert.Equal(new[] { 1 }, Glyphs(Apply(gsub, [1])));
    }

    [Fact]
    public void KeepsItsDirectionThroughAnExtension()
    {
        byte[] gsub = OneLookup(7, Extension(8, ReverseChain(Cover(1), [], [Cover(2)], 2)));

        Assert.Equal(new[] { 2, 2, 2 }, Glyphs(Apply(gsub, [1, 1, 2])));
    }

    [Fact]
    public void RejectsSubstitutesPastTheTable()
    {
        byte[] subtable = ReverseChain(Cover(1), [], [], 7);

        // Format, coverage offset, backtrack count, lookahead count, then the substitute count.
        BigEndian.WriteUInt16(subtable, 8, 5000);

        Assert.Throws<FontFormatException>(() => Apply(OneLookup(8, subtable), [1]));
    }
}
