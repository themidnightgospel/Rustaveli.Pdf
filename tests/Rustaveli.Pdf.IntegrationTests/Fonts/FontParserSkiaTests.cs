using System.Runtime.InteropServices;
using Rustaveli.Pdf.Fonts;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests.Fonts;

/// <summary>
/// The managed parser against Skia, which the layout engine measured with until now: the same fonts must give the
/// same glyphs, advances and line metrics, or switching measurers would move text.
/// </summary>
public class FontParserSkiaTests
{
    /// <summary>
    /// Whether Skia can open a face after the first in a collection file. On macOS it reads fonts through CoreText,
    /// which ignores the face index, so there Skia is no oracle for them and only Windows and Linux compare them.
    /// </summary>
    private static readonly bool SkiaOpensCollectionFaces = !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static TheoryData<string, int> Faces => WithoutUnreadableFaces(new TheoryData<string, int>
    {
        { "NotoSans-Regular.ttf", 0 },
        { "NotoSans-Bold.ttf", 0 },
        { "NotoSans-Italic.ttf", 0 },
        { "NotoSansGeorgian-Regular.ttf", 0 },
        { "SpecimenSans.ttc", 1 },
        { "SpecimenCff-Regular.otf", 0 }
    });

    private static TheoryData<string, int> WithoutUnreadableFaces(TheoryData<string, int> faces)
    {
        TheoryData<string, int> readable = new TheoryData<string, int>();

        foreach (object[] row in faces)
        {
            if (SkiaOpensCollectionFaces || (int)row[1] == 0)
                readable.Add((string)row[0], (int)row[1]);
        }

        return readable;
    }

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

        Assert.Equal(-expected.Ascent, font.ToPoints(metrics.Ascent, 20f), 0.001f);
        Assert.Equal(expected.Descent, font.ToPoints(metrics.Descent, 20f), 0.001f);
        Assert.Equal(expected.Leading, font.ToPoints(metrics.LineGap, 20f), 0.001f);
    }

    [Fact]
    public void SkiaLoadsACollectionFaceWrittenOutOnItsOwn()
    {
        OpenTypeFont face = FontAssets.Load("SpecimenSans.ttc", 1);
        using SKTypeface? typeface = SKTypeface.FromStream(new MemoryStream(face.ToStandaloneFile()));

        Assert.NotNull(typeface);
        Assert.Equal(face.GlyphCount, typeface!.GlyphCount);
        Assert.Equal("Specimen Sans", typeface.FamilyName);
        Assert.Equal(600, typeface.FontWeight);
    }

    [Fact]
    public void CountsTheSameGlyphsAndUnitsPerEm()
    {
        foreach ((string file, int faceIndex) in new[] { ("NotoSans-Regular.ttf", 0), ("SpecimenSans.ttc", 2) })
        {
            if (faceIndex > 0 && !SkiaOpensCollectionFaces)
                continue;

            OpenTypeFont font = FontAssets.Load(file, faceIndex);
            using SKTypeface typeface = SKTypeface.FromFile(FontAssets.PathOf(file), faceIndex);

            Assert.Equal(typeface.GlyphCount, font.GlyphCount);
            Assert.Equal(typeface.UnitsPerEm, font.UnitsPerEm);
        }
    }
}
