using Rustaveli.Pdf.Fonts;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests.Fonts;

/// <summary>
/// The managed parser against Skia, which the layout engine measured with until now: the same fonts must give the
/// same glyphs, advances and line metrics, or switching measurers would move text.
/// </summary>
public class FontParserSkiaTests
{
    public static TheoryData<string, int> Faces => new TheoryData<string, int>
    {
        { "NotoSans-Regular.ttf", 0 },
        { "NotoSans-Bold.ttf", 0 },
        { "NotoSans-Italic.ttf", 0 },
        { "NotoSansGeorgian-Regular.ttf", 0 },
        { "SpecimenSans.ttc", 1 },
        { "SpecimenCff-Regular.otf", 0 }
    };

    private static SKFont LinearFont(SKTypeface typeface, float size) =>
        new SKFont(typeface, size) { LinearMetrics = true, Hinting = SKFontHinting.None, Subpixel = true };

    [Theory]
    [MemberData(nameof(Faces))]
    public void MapsCharactersToTheSameGlyphsWithTheSameAdvances(string file, int faceIndex)
    {
        OpenTypeFont font = FontAssets.Load(file, faceIndex);
        using SKTypeface typeface = SKTypeface.FromFile(FontAssets.PathOf(file), faceIndex);
        using SKFont skia = LinearFont(typeface, font.UnitsPerEm);
        string text = new string(Enumerable.Range(0x20, 0x5F).Select(code => (char)code).ToArray()) + "\u00C5\u10D0";

        ushort[] glyphs = skia.GetGlyphs(text);
        float[] advances = skia.GetGlyphWidths(glyphs);

        for (int index = 0; index < text.Length; index++)
        {
            Assert.Equal(glyphs[index], font.GetGlyphId(text[index]));
            Assert.Equal(advances[index], font.GetAdvance(glyphs[index]), 2);
        }
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public void ReportsTheLineMetricsSkiaDid(string file, int faceIndex)
    {
        OpenTypeFont font = FontAssets.Load(file, faceIndex);
        using SKTypeface typeface = SKTypeface.FromFile(FontAssets.PathOf(file), faceIndex);
        using SKFont skia = LinearFont(typeface, 20f);
        SKFontMetrics expected = skia.Metrics;
        LineMetrics metrics = font.LineMetrics;

        Assert.Equal(-expected.Ascent, font.ToPoints(metrics.Ascent, 20f), 3);
        Assert.Equal(expected.Descent, font.ToPoints(metrics.Descent, 20f), 3);
        Assert.Equal(expected.Leading, font.ToPoints(metrics.LineGap, 20f), 3);
    }

    [Fact]
    public void CountsTheSameGlyphsAndUnitsPerEm()
    {
        foreach ((string file, int faceIndex) in new[] { ("NotoSans-Regular.ttf", 0), ("SpecimenSans.ttc", 2) })
        {
            OpenTypeFont font = FontAssets.Load(file, faceIndex);
            using SKTypeface typeface = SKTypeface.FromFile(FontAssets.PathOf(file), faceIndex);

            Assert.Equal(typeface.GlyphCount, font.GlyphCount);
            Assert.Equal(typeface.UnitsPerEm, font.UnitsPerEm);
        }
    }
}
