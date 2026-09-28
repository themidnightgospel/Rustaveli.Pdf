using Rustaveli.Pdf.Text;
using UglyToad.PdfPig;

namespace Rustaveli.Pdf.IntegrationTests.Output;

/// <summary>
/// One glyph standing for different text: Noto Sans sets ḿ as an m and a combining acute, which there stands for
/// nothing of its own, and draws a typed combining acute with that same glyph, standing for itself.
/// </summary>
public class SharedGlyphTests
{
    private const string DecomposedLetter = "ḿ";
    private const string TypedAccent = "á";

    private static readonly TypefaceLibrary Library = TestFonts.NewLibrary(includeInstalled: false);

    private static byte[] Export(string text, bool compress = true) =>
        Document.Compose(composition => composition.Section(section =>
        {
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            section.Body().Text(text);
        })).ExportPdf(new PdfExportOptions { Typefaces = Library, Compress = compress });

    private static List<ShapedGlyph> Shape(string text)
    {
        List<ShapedGlyph> glyphs = [];

        foreach (ShapedGlyph glyph in Library.Shaper.Walk(text.AsSpan(), TypeStyle.Default.WithTypeface(TestFonts.Sans)))
            glyphs.Add(glyph);

        return glyphs;
    }

    [Fact]
    public void TheAccentOfTheLetterAndTheTypedAccentAreOneGlyph()
    {
        ShapedGlyph silent = Shape(DecomposedLetter)[1];
        ShapedGlyph typed = Shape(TypedAccent)[1];

        Assert.Equal(silent.Glyph, typed.Glyph);
        Assert.Equal(string.Empty, silent.ReadsAs);
        Assert.Equal("́", typed.ReadsAs);
    }

    [Theory]
    [InlineData(DecomposedLetter + " " + TypedAccent)]
    [InlineData(TypedAccent + " " + DecomposedLetter)]
    public void EachReadsBackAsTyped(string text)
    {
        using PdfDocument parsed = PdfDocument.Open(Export(text));

        Assert.Equal(text, parsed.GetPage(1).Text);
    }

    [Fact]
    public void ADocumentWithoutSuchUsesKeepsCodesAsGlyphIds()
    {
        string file = System.Text.Encoding.Latin1.GetString(Export("Plain text", compress: false));

        Assert.Contains("/CIDToGIDMap/Identity", file);
    }

    [Fact]
    public void ADocumentWithSuchUsesMapsItsCodesToTheirGlyphs()
    {
        string file = System.Text.Encoding.Latin1.GetString(Export(DecomposedLetter + TypedAccent, compress: false));

        // The typed accent is the glyph's second use, so it takes the first further code.
        Assert.DoesNotContain("/CIDToGIDMap/Identity", file);
        Assert.Matches(@"/CIDToGIDMap \d+ 0 R", file);
        Assert.Contains("<8000> <0301>", file);
    }
}
