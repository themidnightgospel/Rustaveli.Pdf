using Rustaveli.Pdf.Documents;
using Rustaveli.Pdf.Fluent;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;
using Rustaveli.Pdf.Text;
using SkiaSharp;
using UglyToad.PdfPig;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Exercises supplying typefaces from a stream rather than relying on what the host has installed.
/// </summary>
/// <remarks>
/// The font registered is the committed Noto Sans (tests/assets/fonts), so these behave identically on every host.
/// The providers here start empty on purpose: registration is what is under test.
/// </remarks>
public class FontRegistrationTests
{
    /// <summary>Locates any TrueType font on the host, or null when none is available.</summary>
    private static string FontFile => TestFonts.PathOf("NotoSans-Regular.ttf");

    [Fact]
    public void RegisteredFontIsUsedForMeasurement()
    {
using SkiaFontProvider fonts = new SkiaFontProvider();
        using FileStream stream = File.OpenRead(FontFile);
        fonts.Register(stream);

        SkiaTextMeasurer measurer = new SkiaTextMeasurer(fonts);
        TextStyle style = TextStyle.Default.FontFamilyOf(TestFonts.Sans);

        Assert.Equal(TestFonts.Sans, fonts.GetTypeface(style).FamilyName);
        Assert.True(measurer.GetMetrics(style).Ascent > 0, "A registered font must report a positive ascent.");
        Assert.True(measurer.MeasureWidth("Hello", style) > 0);
    }

    [Fact]
    public void RegisteringFromAStreamDoesNotThrow()
    {
using SkiaFontProvider fonts = new SkiaFontProvider();
        using FileStream stream = File.OpenRead(FontFile);

        fonts.Register(stream);
    }

    [Fact]
    public void RejectsDataThatIsNotAFont()
    {
        using SkiaFontProvider fonts = new SkiaFontProvider();
        using MemoryStream stream = new MemoryStream("this is definitely not a font"u8.ToArray());

        Assert.Throws<InvalidOperationException>(() => fonts.Register(stream));
    }

    [Fact]
    public void DocumentsCanRenderWithAProvidedFontProvider()
    {
using SkiaFontProvider fonts = new SkiaFontProvider();
        using FileStream stream = File.OpenRead(FontFile);
        fonts.Register(stream);

        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = PageSizes.A4;
            page.Margin = Edges.All(30);
            page.Content().Text("Rendered with a registered font");
        }));

        byte[] bytes = document.GeneratePdf(new PdfGenerationOptions { Fonts = fonts });

        using PdfDocument parsed = PdfDocument.Open(bytes);
        Assert.Contains("Rendered", parsed.GetPage(1).Text);
    }

    [Fact]
    public void ReusesTheSameFontInstanceForRepeatedStyles()
    {
        using SkiaFontProvider fonts = new SkiaFontProvider();
        TextStyle style = TextStyle.Default.FontSizeOf(14);

        // Caching matters: the layout engine measures the same styles many times per document.
        Assert.Same(fonts.GetFont(style), fonts.GetFont(style));
    }

    [Fact]
    public void DistinguishesFontsBySizeAndWeight()
    {
        using SkiaFontProvider fonts = new SkiaFontProvider();

        SKFont regular = fonts.GetFont(TextStyle.Default.FontSizeOf(12));
        SKFont larger = fonts.GetFont(TextStyle.Default.FontSizeOf(24));
        SKFont bold = fonts.GetFont(TextStyle.Default.FontSizeOf(12).Bold());

        Assert.NotSame(regular, larger);
        Assert.NotSame(regular, bold);
    }

    [Fact]
    public void FallsBackToADefaultTypefaceForAnUnknownFamily()
    {
        using SkiaFontProvider fonts = new SkiaFontProvider();
        SkiaTextMeasurer measurer = new SkiaTextMeasurer(fonts);

        TextStyle style = TextStyle.Default.FontFamilyOf("A Font That Certainly Does Not Exist");

        // Skia substitutes rather than failing, so text must still measure to something usable.
        Assert.True(measurer.MeasureWidth("Hello", style) > 0);
    }
}
