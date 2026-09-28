using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SyntheticSubstitution;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

public class SingleSubstitutionTests
{
    private static byte[] Cover(params int[] glyphs) => SyntheticLayout.CoverageFormat1(glyphs);

    [Fact]
    public void AddsTheDeltaToCoveredGlyphs()
    {
        GlyphBuffer buffer = Apply(OneLookup(1, SingleFormat1(Cover(2, 4), 10)), [1, 2, 3, 4]);

        Assert.Equal(new[] { 1, 12, 3, 14 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 1, 2, 3 }, Clusters(buffer));
    }

    [Fact]
    public void WrapsTheDeltaModuloSixtyFiveThousandFiveHundredThirtySix()
    {
        GlyphBuffer buffer = Buffer(3, 10);

        Table(OneLookup(1, SingleFormat1(Cover(3, 10), -5)), glyphCount: 65536)
            .Apply(buffer, ScriptTag.Default, LanguageTag.Default, [FeatureSetting.On(Feature)]);

        Assert.Equal(new[] { 65534, 5 }, Glyphs(buffer));
    }

    [Fact]
    public void ListsASubstituteForEachCoveredGlyph()
    {
        GlyphBuffer buffer = Apply(OneLookup(1, SingleFormat2(Cover(2, 3), 20, 30)), [1, 2, 3]);

        Assert.Equal(new[] { 1, 20, 30 }, Glyphs(buffer));
    }

    [Fact]
    public void IgnoresCoverageIndicesPastTheSubstitutes()
    {
        GlyphBuffer buffer = Apply(OneLookup(1, SingleFormat2(Cover(2, 3), 20)), [2, 3]);

        Assert.Equal(new[] { 20, 3 }, Glyphs(buffer));
    }

    [Fact]
    public void RefusesGlyphsTheFontLacks()
    {
        // 1 + 998 is the font's last glyph; 2 + 998 is one past it.
        Assert.Equal(new[] { 999, 2 }, Glyphs(Apply(OneLookup(1, SingleFormat1(Cover(1, 2), 998)), [1, 2])));
        Assert.Equal(new[] { 1 }, Glyphs(Apply(OneLookup(1, SingleFormat2(Cover(1), 1000)), [1])));
    }

    [Fact]
    public void SubstitutesTheMissingGlyphLikeAnyOther()
    {
        Assert.Equal(new[] { 0 }, Glyphs(Apply(OneLookup(1, SingleFormat1(Cover(1), -1)), [1])));
    }

    [Fact]
    public void TriesTheNextSubtableWhereOneDoesNotApply()
    {
        byte[] gsub = OneLookup(1, SingleFormat2(Cover(2), 20), SingleFormat1(Cover(2, 3), 100));

        Assert.Equal(new[] { 20, 103 }, Glyphs(Apply(gsub, [2, 3])));
    }

    [Fact]
    public void RejectsSubstitutesPastTheTable()
    {
        byte[] subtable = SingleFormat2(Cover(2), 20);
        BigEndian.WriteUInt16(subtable, 4, 5000);

        Assert.Throws<FontFormatException>(() => Apply(OneLookup(1, subtable), [2]));
    }
}
