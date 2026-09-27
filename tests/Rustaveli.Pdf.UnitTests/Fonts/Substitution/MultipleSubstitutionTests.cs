using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SyntheticSubstitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

public class MultipleSubstitutionTests
{
    /// <summary>An empty sequence, which deletes the glyph.</summary>
    private static readonly int[] NoGlyphs = [];

    private static byte[] Cover(params int[] glyphs) => SyntheticLayout.CoverageFormat1(glyphs);

    [Fact]
    public void ReplacesAGlyphWithSeveralInItsCluster()
    {
        GlyphBuffer buffer = Apply(OneLookup(2, Multiple(Cover(2), [5, 6, 7])), [1, 2, 3]);

        Assert.Equal(new[] { 1, 5, 6, 7, 3 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 1, 1, 1, 2 }, Clusters(buffer));
    }

    [Fact]
    public void ContinuesAfterTheGlyphsItInserted()
    {
        // Glyph 1 becomes 1 and 2; were the new 1 substituted again, the text would never stop growing.
        GlyphBuffer buffer = Apply(OneLookup(2, Multiple(Cover(1), [1, 2])), [1, 1]);

        Assert.Equal(new[] { 1, 2, 1, 2 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 0, 1, 1 }, Clusters(buffer));
    }

    [Fact]
    public void ReplacesAGlyphWithOne()
    {
        Assert.Equal(new[] { 9, 2 }, Glyphs(Apply(OneLookup(2, Multiple(Cover(1), [9])), [1, 2])));
    }

    [Fact]
    public void DeletesAGlyphForAnEmptySequenceJoiningItsCharactersToTheNextAtTheStart()
    {
        GlyphBuffer buffer = Apply(OneLookup(2, Multiple(Cover(2), NoGlyphs)), [2, 1, 3]);

        Assert.Equal(new[] { 1, 3 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 2 }, Clusters(buffer));
    }

    [Fact]
    public void DeletesAGlyphJoiningItsCharactersToThePreviousCluster()
    {
        GlyphBuffer buffer = new GlyphBuffer();

        // Out of order, as a caller may set clusters, so which neighbour the characters join shows.
        foreach ((int glyph, int cluster) in new[] { (4, 0), (1, 5), (2, 3), (3, 8) })
            buffer.Add((ushort)glyph, cluster);

        Table(OneLookup(2, Multiple(Cover(2), NoGlyphs)))
            .Apply(buffer, ScriptTag.Default, LanguageTag.Default, [FeatureSetting.On(Feature)]);

        Assert.Equal(new[] { 4, 1, 3 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 3, 8 }, Clusters(buffer));
    }

    [Fact]
    public void DeletesEveryCoveredGlyphEvenTheLast()
    {
        // Sized exactly, so nothing past the one glyph could be read unnoticed.
        GlyphBuffer single = new GlyphBuffer(1);
        single.Add(2, 0);

        Table(OneLookup(2, Multiple(Cover(2), NoGlyphs)))
            .Apply(single, ScriptTag.Default, LanguageTag.Default, [FeatureSetting.On(Feature)]);

        Assert.Equal(0, single.Count);
        Assert.Empty(Glyphs(Apply(OneLookup(2, Multiple(Cover(2), NoGlyphs)), [2, 2])));
    }

    [Fact]
    public void RefusesASequenceWithAGlyphTheFontLacks()
    {
        GlyphBuffer buffer = Apply(OneLookup(2, Multiple(Cover(2), [5, 1000])), [2]);

        Assert.Equal(new[] { 2 }, Glyphs(buffer));
    }

    [Fact]
    public void IgnoresCoverageIndicesPastTheSequences()
    {
        GlyphBuffer buffer = Apply(OneLookup(2, Multiple(Cover(2, 3), [5, 6])), [2, 3]);

        Assert.Equal(new[] { 5, 6, 3 }, Glyphs(buffer));
    }

    [Theory]
    [InlineData(5, new[] { 1, 2, 3, 1 })]
    [InlineData(6, new[] { 1, 2, 3, 1, 2, 3 })]
    public void StopsGrowingTheTextAtItsLimit(int maximumLength, int[] expected)
    {
        byte[] gsub = OneLookup(2, Multiple(Cover(1), [1, 2, 3]));

        Assert.Equal(expected, Glyphs(ApplyWithLimits(gsub, [1, 1], 1000, maximumLength)));
    }

    [Fact]
    public void RejectsSequencesPastTheTable()
    {
        byte[] subtable = Multiple(Cover(2), [5]);
        BigEndian.WriteUInt16(subtable, 4, 5000);

        Assert.Throws<FontFormatException>(() => Apply(OneLookup(2, subtable), [2]));
    }
}
