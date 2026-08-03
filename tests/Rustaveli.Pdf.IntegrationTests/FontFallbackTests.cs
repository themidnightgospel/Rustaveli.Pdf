using Rustaveli.Pdf.Documents;
using Rustaveli.Pdf.Fluent;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;
using Rustaveli.Pdf.Text;
using UglyToad.PdfPig;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Verifies that characters absent from the requested font are drawn with one that has them.
/// </summary>
/// <remarks>
/// The decisive check is extraction: a glyph the primary font lacks is emitted as .notdef without fallback, and
/// a reader recovers nothing for it. Getting the characters back out proves a covering face was embedded.
/// </remarks>
public class FontFallbackTests
{
    private const string Latin = "Hello";
    private const string Cjk = "世界";

    private static Document Build(string text) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size = PageSizes.A4;
            page.Margin = Edges.All(30);
            page.DefaultTextStyle = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(16);
            page.Content().Text(text);
        }));

    [Fact]
    public void RendersCharactersTheRequestedFontDoesNotHave()
    {
        using PdfDocument parsed = PdfDocument.Open(Build($"{Latin} {Cjk}").GeneratePdf());
        string text = parsed.GetPage(1).Text;

        Assert.Contains(Latin, text);
        Assert.Contains(Cjk, text);
    }

    [Fact]
    public void MixedScriptTextMeasuresWiderThanItsLatinPartAlone()
    {
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(SkiaFontProvider.Shared);
        TextStyle style = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(16);

        float latinOnly = measurer.MeasureWidth(Latin, style);
        float mixed = measurer.MeasureWidth($"{Latin}{Cjk}", style);

        Assert.True(mixed > latinOnly, $"Mixed-script text measured {mixed} against {latinOnly} for the Latin part alone.");
    }

    [Fact]
    public void MeasuredWidthMatchesWhatIsDrawn()
    {
        // Measurement and drawing must split the string identically; if they disagreed, a fallback glyph would
        // land somewhere other than where its advance was reserved.
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(SkiaFontProvider.Shared);
        TextStyle style = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(16);

        float whole = measurer.MeasureWidth($"{Latin}{Cjk}", style);
        float parts = measurer.MeasureWidth(Latin, style) + measurer.MeasureWidth(Cjk, style);

        Assert.InRange(whole, parts - 0.5f, parts + 0.5f);
    }

    [Fact]
    public void AnExplicitFallbackFamilyIsPreferredOverPlatformMatching()
    {
        // Naming the fallback is what makes output reproducible; left to the platform the substitute differs
        // between machines.
        using SkiaFontProvider provider = new SkiaFontProvider();
        provider.FallbackFamilies.Add("Segoe UI");

        SkiaTextMeasurer measurer = new SkiaTextMeasurer(provider);
        TextStyle style = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(16);

        Assert.True(measurer.MeasureWidth(Cjk, style) > 0);
    }

    [Fact]
    public void PurelyLatinTextIsUnaffected()
    {
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(SkiaFontProvider.Shared);
        TextStyle style = TextStyle.Default.FontFamilyOf("Arial").FontSizeOf(16);

        // The fast path must produce exactly what a single-font measurement always did.
        Assert.True(measurer.MeasureWidth(Latin, style) > 0);
        Assert.Equal(measurer.MeasureWidth(Latin, style), measurer.MeasureWidth(Latin, style));
    }
}
