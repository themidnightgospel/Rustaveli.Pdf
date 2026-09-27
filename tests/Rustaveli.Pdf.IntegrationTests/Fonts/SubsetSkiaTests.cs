using Rustaveli.Pdf.Fonts;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests.Fonts;

/// <summary>
/// Subset fonts checked by an independent reader: Skia loads the subset bytes through the platform's own font stack
/// (DirectWrite, FreeType or Core Text), maps characters through the subset's character map, and measures and bounds
/// its glyphs. Agreement with the original font shows the outlines, metrics and renumbering survived subsetting.
/// </summary>
public class SubsetSkiaTests
{
    private const string Text = "Hello, World! \u00C5";

    private static SKTypeface LoadTypeface(byte[] data)
    {
        SKTypeface? typeface = SKTypeface.FromStream(new MemoryStream(data));
        Assert.NotNull(typeface);
        return typeface!;
    }

    /// <summary>A font at one unit per point, so Skia's unhinted measurements come back in font units.</summary>
    private static SKFont AtUnitsPerEm(SKTypeface typeface, int unitsPerEm) =>
        new SKFont(typeface, unitsPerEm) { LinearMetrics = true, Hinting = SKFontHinting.None, Subpixel = true };

    [Fact]
    public void SkiaLoadsTheSubsetWithEveryGlyphItNeeds()
    {
        OpenTypeFont font = FontAssets.Load("NotoSans-Regular.ttf");
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, Text.Select(character => font.GetGlyphId(character)));

        using SKTypeface typeface = LoadTypeface(subset.FontData);

        // .notdef, space, the ten distinct letters and punctuation, Å and the ring component Å is built from.
        Assert.Equal(14, subset.GlyphCount);
        Assert.Equal(subset.GlyphCount, typeface.GlyphCount);
    }

    [Fact]
    public void SubsetCharactersMapToTheRenumberedGlyphsWithTheOriginalAdvances()
    {
        OpenTypeFont font = FontAssets.Load("NotoSans-Regular.ttf");
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, Text.Select(character => font.GetGlyphId(character)));
        using SKTypeface typeface = LoadTypeface(subset.FontData);
        using SKFont skia = AtUnitsPerEm(typeface, font.UnitsPerEm);

        ushort[] glyphs = skia.GetGlyphs(Text);
        float[] widths = skia.GetGlyphWidths(glyphs);

        for (int index = 0; index < Text.Length; index++)
        {
            ushort original = font.GetGlyphId(Text[index]);

            Assert.Equal(subset.GlyphIdMap[original], glyphs[index]);
            Assert.Equal(font.GetAdvance(original), widths[index], 0.01f);
        }
    }

    [Theory]
    [InlineData('\u00C5')]
    [InlineData('\uE000')]
    [InlineData('\uE001')]
    [InlineData('\uE002')]
    [InlineData('\uE003')]
    public void CompositeGlyphsKeepTheirShapeThroughRenumbering(char character)
    {
        // Specimen Sans has a nested composite (Å plus an acute) and one composite per scale encoding. A component
        // renumbered wrongly, or a record misread for its flags, draws a different glyph with different bounds.
        string path = FontAssets.PathOf("SpecimenSans.ttc");
        OpenTypeFont font = FontAssets.Load("SpecimenSans.ttc");
        ushort original = font.GetGlyphId(character);
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, [original]);

        using SKTypeface originalFace = SKTypeface.FromFile(path, 0);
        using SKTypeface subsetFace = LoadTypeface(subset.FontData);
        using SKFont originalFont = AtUnitsPerEm(originalFace, font.UnitsPerEm);
        using SKFont subsetFont = AtUnitsPerEm(subsetFace, font.UnitsPerEm);

        using SKPath originalPath = originalFont.GetGlyphPath(original);
        using SKPath subsetPath = subsetFont.GetGlyphPath(subset.GlyphIdMap[original]);
        SKRect expected = originalPath.TightBounds;
        SKRect actual = subsetPath.TightBounds;

        Assert.True(expected.Width > 0, "The glyph should have an outline to compare.");
        Assert.Equal(expected.Left, actual.Left, 2);
        Assert.Equal(expected.Top, actual.Top, 2);
        Assert.Equal(expected.Right, actual.Right, 2);
        Assert.Equal(expected.Bottom, actual.Bottom, 2);
    }

    [Fact]
    public void AGlyphSubsetKeepsTheNumbersItHandedOut()
    {
        OpenTypeFont font = FontAssets.Load("NotoSansGeorgian-Regular.ttf");
        GlyphSubset collector = new GlyphSubset(font);
        const string Georgian = "\u10E1\u10D0\u10E5\u10D0\u10E0\u10D7\u10D5\u10D4\u10DA\u10DD";
        ushort[] numbers = Georgian.Select(character => collector.Add(font.GetGlyphId(character), character)).ToArray();

        TrueTypeSubset subset = collector.Build();
        using SKTypeface typeface = LoadTypeface(subset.FontData);
        using SKFont skia = AtUnitsPerEm(typeface, font.UnitsPerEm);

        Assert.Equal(numbers, skia.GetGlyphs(Georgian));
    }
}
