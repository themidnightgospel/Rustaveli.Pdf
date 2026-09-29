using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SyntheticSubstitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

public class LigatureSubstitutionTests
{
    private const int F = 1;
    private const int I = 2;
    private const int L = 3;
    private const int Acute = 4;
    private const int Space = 5;
    private const int FI = 10;
    private const int FFI = 11;
    private const int FL = 12;
    private const int FF = 13;

    private static byte[] Cover(params int[] glyphs) => SyntheticLayout.CoverageFormat1(glyphs);

    /// <summary>f's ligatures, longest first, as fonts list them so the longest one wins.</summary>
    private static byte[] Ligatures(int flags = 0) => SyntheticSubstitution.Gsub([Lookup(4, flags, 0,
        Ligature(Cover(F), [(FFI, [F, I]), (FI, [I]), (FL, [L]), (FF, [F])]))]);

    /// <summary>Glyph 4 is a mark; the others are bases.</summary>
    private static byte[] MarkClasses() => SyntheticLayout.Gdef(SyntheticLayout.ClassFormat1(1, 1, 1, 1, 3));

    [Fact]
    public void FormsLigaturesThatStandForEveryCharacterTheyReplace()
    {
        GlyphBuffer buffer = Apply(Ligatures(), [F, F, I, Space, F, L, Space, F, F]);

        Assert.Equal(new[] { FFI, Space, FL, Space, FF }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 3, 4, 6, 7 }, Clusters(buffer));
    }

    [Fact]
    public void FormsTheFirstLigatureListedThatMatches()
    {
        byte[] gsub = OneLookup(4, Ligature(Cover(F), [(FF, [F]), (FFI, [F, I])]));

        GlyphBuffer buffer = Apply(gsub, [F, F, I]);

        Assert.Equal(new[] { FF, I }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 2 }, Clusters(buffer));
    }

    [Fact]
    public void ContinuesAfterTheLigature()
    {
        Assert.Equal(new[] { FI, FI }, Glyphs(Apply(Ligatures(), [F, I, F, I])));
    }

    [Theory]
    [InlineData(new[] { F, Space })]
    [InlineData(new[] { F })]
    [InlineData(new[] { Space, F })]
    public void LeavesGlyphsThatBeginNoLigature(int[] glyphs)
    {
        Assert.Equal(glyphs, Glyphs(Apply(Ligatures(), glyphs)));
    }

    [Fact]
    public void TriesTheNextLigatureWhenOneIsNotInTheFont()
    {
        byte[] gsub = OneLookup(4, Ligature(Cover(F), [(1000, [I]), (FI, [I])]));

        Assert.Equal(new[] { FI }, Glyphs(Apply(gsub, [F, I])));
    }

    [Fact]
    public void TreatsALigatureOfOneComponentAsASingleSubstitution()
    {
        byte[] gsub = OneLookup(4, Ligature(Cover(F), [(FI, [])]));

        Assert.Equal(new[] { FI, I }, Glyphs(Apply(gsub, [F, I])));
    }

    [Fact]
    public void TreatsALigatureOfNoComponentsAsASingleSubstitution()
    {
        byte[] subtable = Ligature(Cover(F), [(FI, [])]);

        // The one ligature's component count: format, coverage, count, set offset, coverage (6), set (4), glyph.
        BigEndian.WriteUInt16(subtable, 8 + 6 + 4 + 2, 0);

        Assert.Equal(new[] { FI, I }, Glyphs(Apply(OneLookup(4, subtable), [F, I])));
    }

    [Fact]
    public void IgnoresCoverageIndicesPastTheSets()
    {
        byte[] gsub = OneLookup(4, Ligature(Cover(F, L), [(FI, [I])]));

        Assert.Equal(new[] { L, I }, Glyphs(Apply(gsub, [L, I])));
    }

    [Fact]
    public void KeepsTheMarksItPassesOverAfterTheLigatureInItsCluster()
    {
        GlyphBuffer buffer = Apply(Ligatures(flags: 0x0008), [F, Acute, I, Space], MarkClasses());

        Assert.Equal(new[] { FI, Acute, Space }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 0, 3 }, Clusters(buffer));
    }

    [Fact]
    public void DoesNotReachPastAMarkItDoesNotPassOver()
    {
        GlyphBuffer buffer = Apply(Ligatures(), [F, Acute, I], MarkClasses());

        Assert.Equal(new[] { F, Acute, I }, Glyphs(buffer));
    }

    [Fact]
    public void MergesAClusterAnEarlierSubstitutionSplit()
    {
        // Glyph 6 becomes 7 and f; the f then forms fi with the next glyph, so all three characters' glyphs are one
        // cluster.
        byte[] gsub = SyntheticSubstitution.Gsub(
            [Lookup(2, Multiple(Cover(6), [7, F])), Lookup(4, Ligature(Cover(F), [(FI, [I])]))], 0, 1);

        GlyphBuffer buffer = Apply(gsub, [6, I, Space]);

        Assert.Equal(new[] { 7, FI, Space }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 0, 2 }, Clusters(buffer));
    }

    [Theory]
    [InlineData(1, new[] { F, I })]
    [InlineData(2, new[] { F, I })]
    [InlineData(3, new[] { FI })]
    public void StopsTryingLigaturesOnceTheWorkIsSpent(int work, int[] expected)
    {
        // One unit to try the subtable, one for the ligature, one to step to its second component.
        byte[] gsub = OneLookup(4, Ligature(Cover(F), [(FI, [I])]));

        Assert.Equal(expected, Glyphs(ApplyWithLimits(gsub, [F, I], work, 100)));
    }

    [Fact]
    public void RejectsLigatureSetsPastTheTable()
    {
        byte[] subtable = Ligature(Cover(F), [(FI, [I])]);
        BigEndian.WriteUInt16(subtable, 4, 5000);

        Assert.Throws<FontFormatException>(() => ReadSubtable(4, subtable));
    }
}
