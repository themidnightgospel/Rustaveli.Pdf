using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests.Output;

/// <summary>
/// Documents written as PDF/A: described in XMP, shown through an sRGB output intent, and every colour in RGB.
/// </summary>
public class PdfAOutputTests
{
    private static readonly Ink Process = Ink.Cmyk(0.1f, 0.2f, 0.3f, 0.4f);

    private static string ImagePath(string name) => Path.Combine(AppContext.BaseDirectory, "assets", "images", name);

    private static string Export(Action<StackComposer> compose, PdfAConformance conformance = PdfAConformance.PdfA2B, IImageProcessor? processor = null)
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(300, 300);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            section.Body().Stack(compose);
        }));

        document.Info.Title = "Archive & keep";
        document.Info.Author = "Registry";
        document.Info.Subject = "Records";
        document.Info.Keywords = "a, b";
        document.Info.Creator = "Tests";
        document.Info.Producer = "Rustaveli.Pdf";
        document.Info.CreationDate = new DateTimeOffset(2026, 9, 28, 10, 30, 15, TimeSpan.FromHours(4));
        document.Info.ModificationDate = new DateTimeOffset(2026, 9, 28, 11, 0, 0, TimeSpan.Zero);

        return Encoding.Latin1.GetString(document.ExportPdf(new PdfExportOptions { Conformance = conformance, Compress = false, ImageProcessor = processor }));
    }

    [Theory]
    [InlineData(PdfAConformance.PdfA2B, "2", "B")]
    [InlineData(PdfAConformance.PdfA2U, "2", "U")]
    [InlineData(PdfAConformance.PdfA3B, "3", "B")]
    [InlineData(PdfAConformance.PdfA3U, "3", "U")]
    public void TheMetadataClaimsThePartAndLevel(PdfAConformance conformance, string part, string level)
    {
        string pdf = Export(stack => stack.Add().Text("Kept"), conformance);

        Assert.Contains($"<pdfaid:part>{part}</pdfaid:part>", pdf, StringComparison.Ordinal);
        Assert.Contains($"<pdfaid:conformance>{level}</pdfaid:conformance>", pdf, StringComparison.Ordinal);
        Assert.Matches(@"/Type\s*/Metadata\s*/Subtype\s*/XML", pdf);
    }

    [Fact]
    public void TheMetadataSaysWhatTheInformationDictionarySays()
    {
        string pdf = Export(stack => stack.Add().Text("Kept"));

        Assert.Contains("<rdf:li xml:lang=\"x-default\">Archive &amp; keep</rdf:li>", pdf, StringComparison.Ordinal);
        Assert.Contains("<dc:creator><rdf:Seq><rdf:li>Registry</rdf:li></rdf:Seq></dc:creator>", pdf, StringComparison.Ordinal);
        Assert.Contains("<rdf:li xml:lang=\"x-default\">Records</rdf:li>", pdf, StringComparison.Ordinal);
        Assert.Contains("<pdf:Keywords>a, b</pdf:Keywords>", pdf, StringComparison.Ordinal);
        Assert.Contains("<pdf:Producer>Rustaveli.Pdf</pdf:Producer>", pdf, StringComparison.Ordinal);
        Assert.Contains("<xmp:CreatorTool>Tests</xmp:CreatorTool>", pdf, StringComparison.Ordinal);
        Assert.Contains("<xmp:CreateDate>2026-09-28T10:30:15+04:00</xmp:CreateDate>", pdf, StringComparison.Ordinal);
        Assert.Contains("<xmp:ModifyDate>2026-09-28T11:00:00+00:00</xmp:ModifyDate>", pdf, StringComparison.Ordinal);
        Assert.Matches(@"/CreationDate\s*\(D:20260928103015\+04'00'?\)", pdf);
    }

    [Fact]
    public void ColoursAreShownThroughAnSrgbOutputIntent()
    {
        string pdf = Export(stack => stack.Add().Text("Kept"));

        Assert.Matches(@"/OutputIntents\s*\[\s*<<\s*/Type\s*/OutputIntent\s*/S\s*/GTS_PDFA1", pdf);
        Assert.Matches(@"/OutputConditionIdentifier\s*\(sRGB IEC61966-2\.1\)", pdf);
        Assert.Matches(@"/DestOutputProfile \d+ 0 R", pdf);
    }

    [Fact]
    public void TheOutputProfileIsSrgb()
    {
        using SKColorSpace? space = SKColorSpace.CreateIcc(Rustaveli.Pdf.Output.SrgbProfile.Bytes);

        Assert.NotNull(space);
        Assert.True(space!.IsSrgb || space.GammaIsCloseToSrgb, "The profile's curve and primaries should be sRGB's.");
    }

    [Fact]
    public void EveryInkIsWrittenInRgb()
    {
        string pdf = Export(stack =>
        {
            stack.Add().Height(20).Fill(Process).Blank();
            stack.Add().Height(20).Fill(Ink.Spot("Signal", Process)).Blank();
            stack.Add().Height(20).Fill(Gradient.Across(Process, Ink.Cmyk(0, 0, 0, 1))).Blank();
            stack.Add().Height(20).DropShadow(Process, 4).Blank();
            stack.Add().Text(text => text.Run("Black text"));
        });

        Assert.DoesNotMatch(@"\s[kK]\s", pdf);
        Assert.DoesNotContain("/DeviceCMYK", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/Separation", pdf, StringComparison.Ordinal);
        Assert.Matches(@"0\.54 0\.48 0\.42 rg", pdf);
    }

    [Fact]
    public void LinksArePrintedWithThePage()
    {
        string pdf = Export(stack => stack.Add().Link("https://example.com").Text("Linked"));

        Assert.Matches(@"/Subtype\s*/Link[\s\S]*?/F 4", pdf);
    }

    [Fact]
    public void EveryGlyphMustBeFound()
    {
        Document document = Document.Compose(composition => composition.Section(section =>
            section.Body().Text(text => text.Run("").Typeface(TestFonts.Sans))));

        Assert.Throws<MissingGlyphException>(() => document.ExportPdf(new PdfExportOptions
        {
            Conformance = PdfAConformance.PdfA2B,
            Typefaces = TestFonts.NewLibrary(includeInstalled: false),
        }));
    }

    [Fact]
    public void ACmykImageWithoutAProfileBecomesRgb()
    {
        RasterImage image = RasterImage.FromFile(ImagePath("jpeg-cmyk-adobe.jpg"));

        string pdf = Export(stack => stack.Add().Width(40).Image(image), processor: SkiaImageProcessor.Instance);

        Assert.Contains("/DCTDecode", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/DeviceCMYK", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void ACmykImageWithoutAProfileNeedsAProcessor()
    {
        RasterImage image = RasterImage.FromFile(ImagePath("jpeg-cmyk-adobe.jpg"));

        RenderingException exception = Assert.Throws<RenderingException>(() => Export(stack => stack.Add().Width(40).Image(image)));

        InvalidOperationException cause = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("PDF/A", cause.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnRgbImageIsEmbeddedAsItIs()
    {
        string pdf = Export(stack => stack.Add().Width(40).Image(RasterImage.FromFile(ImagePath("jpeg-baseline.jpg"))));

        Assert.Single(Regex.Matches(pdf, "/DCTDecode"));
    }

    [Fact]
    public void APlainPdfHasNoneOfThis()
    {
        string pdf = Export(stack => stack.Add().Height(20).Fill(Process).Blank(), PdfAConformance.None);

        Assert.DoesNotContain("pdfaid", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/OutputIntents", pdf, StringComparison.Ordinal);
        Assert.Matches(@"0\.1 0\.2 0\.3 0\.4 k", pdf);
    }
}
