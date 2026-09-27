using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Fonts.Substitution;
using static Rustaveli.Pdf.UnitTests.Fonts.Substitution.SubstitutionHarness;

namespace Rustaveli.Pdf.UnitTests.Fonts.Substitution;

/// <summary>
/// Noto Sans Regular's own substitutions. Glyph ids were read from the font with fontTools; where no Unicode
/// normalization is involved, HarfBuzz shapes the same text to the same glyphs.
/// </summary>
public class NotoSansSubstitutionTests
{
    private static GlyphBuffer Shape(
        string text, ScriptTag script, LanguageTag language, params FeatureSetting[] features)
    {
        OpenTypeFont font = TestFonts.Regular;
        GlyphBuffer buffer = GlyphBuffer.FromText(font, text);
        font.Substitutions!.Apply(buffer, script, language, features);
        return buffer;
    }

    private static GlyphBuffer Shape(string text, params FeatureSetting[] features) =>
        Shape(text, ScriptTag.Latin, LanguageTag.Default, features);

    /// <summary>The characters the glyph's cluster stands for, as a ToUnicode map would record them.</summary>
    private static string CharactersOf(string text, GlyphBuffer buffer, int glyph)
    {
        int start = buffer.Clusters[glyph];
        return text.Substring(start, buffer.GetClusterEnd(glyph, text.Length) - start);
    }

    private static GlyphBuffer ShapeByDefault(string text, ScriptTag script, string language = "dflt") =>
        Shape(text, script, LanguageTag.Parse(language), [.. GlyphSubstitutionTable.DefaultFeatures]);

    [Fact]
    public void SetsOfficeWithTheFfiLigatureStandingForThreeCharacters()
    {
        const string Text = "office";
        GlyphBuffer buffer = ShapeByDefault(Text, ScriptTag.Latin);

        // o, f_f_i, c, e.
        Assert.Equal(new[] { 82, 1656, 70, 72 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 1, 4, 5 }, Clusters(buffer));
        Assert.Equal("ffi", CharactersOf(Text, buffer, 1));
        Assert.Equal("e", CharactersOf(Text, buffer, 3));
    }

    [Fact]
    public void FormsEveryStandardLigature()
    {
        GlyphBuffer buffer = Shape("fi ffl fl ff", FeatureSetting.On(FeatureTag.StandardLigatures));

        Assert.Equal(new[] { 1654, 3, 1657, 3, 1655, 3, 1653 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 2, 3, 6, 7, 9, 10 }, Clusters(buffer));
    }

    [Fact]
    public void LeavesLigaturesUnformedWhenTurnedOff()
    {
        GlyphBuffer buffer = Shape(
            "office",
            ScriptTag.Latin,
            LanguageTag.Default,
            [.. GlyphSubstitutionTable.DefaultFeatures, FeatureSetting.Off(FeatureTag.StandardLigatures)]);

        Assert.Equal(new[] { 82, 73, 73, 76, 70, 72 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, Clusters(buffer));
    }

    [Theory]
    [InlineData("í", new[] { 1768, 2665 }, new[] { 0, 1 })]
    [InlineData("ị́", new[] { 1768, 2696, 2665 }, new[] { 0, 1, 2 })]
    [InlineData("ị", new[] { 76, 2696 }, new[] { 0, 1 })]
    public void DropsTheDotOfAnIBeforeAMarkAboveItPassingOverMarksBelow(string text, int[] glyphs, int[] clusters)
    {
        // The composition lookup is chained, format 3, and filters marks by a set holding the marks above.
        GlyphBuffer buffer = ShapeByDefault(text, ScriptTag.Latin);

        Assert.Equal(glyphs, Glyphs(buffer));
        Assert.Equal(clusters, Clusters(buffer));
    }

    [Fact]
    public void PassesOverAMarkOutsideTheLookupsFilteringSet()
    {
        // The font lists a and an ogonek as a ligature, in a lookup whose mark filtering set lacks the ogonek: the
        // lookup never sees it, so both glyphs stay. (A shaper composing characters first sets ą from the character
        // map instead, before substitution.)
        GlyphBuffer buffer = ShapeByDefault("ą", ScriptTag.Latin);

        Assert.Equal(new[] { 68, 2700 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 1 }, Clusters(buffer));
    }

    [Fact]
    public void DecomposesALetterIntoItsBaseAndAccent()
    {
        GlyphBuffer buffer = ShapeByDefault("ḿ", ScriptTag.Latin);

        // m and a combining acute, both standing for the one character.
        Assert.Equal(new[] { 80, 2665 }, Glyphs(buffer));
        Assert.Equal(new[] { 0, 0 }, Clusters(buffer));
    }

    [Fact]
    public void SlashesAZeroFollowedByAVariationSelector()
    {
        GlyphBuffer buffer = ShapeByDefault("0︀", ScriptTag.Latin);

        Assert.Equal(new[] { 2251 }, Glyphs(buffer));
    }

    [Fact]
    public void ComposesGreekOnlyForTheGreekScript()
    {
        const string Text = "ᾱ̓̀";

        Assert.Equal(new[] { 3270 }, Glyphs(ShapeByDefault(Text, ScriptTag.Greek)));
        Assert.Equal(new[] { 381, 2672, 2680, 2664 }, Glyphs(ShapeByDefault(Text, ScriptTag.Latin)));
    }

    [Fact]
    public void UsesTheFormsALanguagePrefers()
    {
        // Serbian has its own б; Catalan sets l·l with a raised dot.
        Assert.Equal(new[] { 2092, 459 }, Glyphs(ShapeByDefault("бг", ScriptTag.Cyrillic, "SRB ")));
        Assert.Equal(new[] { 457, 459 }, Glyphs(ShapeByDefault("бг", ScriptTag.Cyrillic)));

        GlyphBuffer catalan = ShapeByDefault("l·l", ScriptTag.Latin, "CAT ");

        Assert.Equal(new[] { 257, 79 }, Glyphs(catalan));
        Assert.Equal(new[] { 0, 2 }, Clusters(catalan));
        Assert.Equal(new[] { 79, 121, 79 }, Glyphs(ShapeByDefault("l·l", ScriptTag.Latin)));
    }

    [Fact]
    public void SetsFractions()
    {
        Assert.Equal(new[] { 2273, 533, 2264 }, Glyphs(Shape("1/2", FeatureSetting.On(FeatureTag.Fractions))));
        Assert.Equal(
            new[] { 2273, 2274, 533, 2265, 2266 }, Glyphs(Shape("12/34", FeatureSetting.On(FeatureTag.Fractions))));
    }

    [Fact]
    public void SetsSmallCapitalsAndFigureStyles()
    {
        Assert.Equal(new[] { 1868, 1881, 1882 }, Glyphs(Shape("abc", FeatureSetting.On(FeatureTag.SmallCapitals))));
        Assert.Equal(
            new[] { 1868, 1881, 1882 }, Glyphs(Shape("ABC", FeatureSetting.On(FeatureTag.CapitalsToSmallCapitals))));
        Assert.Equal(
            new[] { 2221, 2222, 2223, 2224 }, Glyphs(Shape("0123", FeatureSetting.On(FeatureTag.OldstyleFigures))));
        Assert.Equal(new[] { 43, 2254, 50 }, Glyphs(Shape("H2O", FeatureSetting.On(FeatureTag.Subscript))));
        Assert.Equal(new[] { 91, 116 }, Glyphs(Shape("x2", FeatureSetting.On(FeatureTag.Superscript))));
    }

    [Theory]
    [InlineData(1, 2394)]
    [InlineData(2, 2404)]
    [InlineData(3, 2417)]
    [InlineData(4, 11)]
    public void ChoosesAmongAccessAllAlternatesByValue(int value, int expected)
    {
        GlyphBuffer buffer = Shape("(", new FeatureSetting(FeatureTag.Parse("aalt"), value));

        Assert.Equal(new[] { expected }, Glyphs(buffer));
    }
}
