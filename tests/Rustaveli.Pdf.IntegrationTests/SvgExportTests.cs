using System.Xml.Linq;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Pages exported as SVG documents.
/// </summary>
public class SvgExportTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static Document TwoPages() => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = new Extent(200, 100);
        section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(14);
        section.Body().Stack(stack =>
        {
            stack.Add().Height(40).Fill(Ink.Rgb(220, 20, 20)).Text("Outlined");
            stack.Add().Width(20).Image(RasterImage.FromBytes(TestImages.Png(4, 2)));
            stack.Add().NewPage();
            stack.Add().Text("Second page");
        });
    }));

    [Fact]
    public void EachPageIsAnSvgDocumentOfItsSize()
    {
        IReadOnlyList<string> pages = TwoPages().ExportSvg();

        Assert.Equal(2, pages.Count);

        foreach (string page in pages)
        {
            XElement root = XDocument.Parse(page).Root!;
            Assert.Equal(Svg + "svg", root.Name);
            Assert.Equal(("200", "100"), ((string?)root.Attribute("width"), (string?)root.Attribute("height")));
        }
    }

    [Fact]
    public void TextIsDrawnAsOutlinesAndImagesAreCarriedWithin()
    {
        XDocument page = XDocument.Parse(TwoPages().ExportSvg()[0]);

        Assert.Empty(page.Descendants(Svg + "text"));
        Assert.True(page.Descendants(Svg + "path").Count() > 5);
        Assert.Contains(page.Descendants(Svg + "image"), image => image.Attributes().Any(attribute => attribute.Value.StartsWith("data:image/", StringComparison.Ordinal)));
    }

    [Fact]
    public void AnExportedPageReadsBackAsArtwork()
    {
        Artwork artwork = Artwork.FromSvg(TwoPages().ExportSvg()[1]);

        // A unit to the point, read back as CSS pixels.
        Assert.Equal(new Extent(150, 75), artwork.Size);
    }

    [Fact]
    public void PagesAreWrittenWhereTheCallerSays()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"svg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);

        try
        {
            TwoPages().ExportSvg(page => Path.Combine(folder, $"page-{page}.svg"));

            Assert.Equal(["page-1.svg", "page-2.svg"], Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void MissingGlyphsCanBeRefused()
    {
        Document document = Document.Compose(composition => composition.Section(section =>
            section.Body().Text(text => text.Run("").Typeface(TestFonts.Sans))));

        Assert.Throws<MissingGlyphException>(() => document.ExportSvg(new SvgExportOptions { RequireEveryGlyph = true, Typefaces = TestFonts.NewLibrary(includeInstalled: false) }));
    }

    [Fact]
    public void GeneratedImagesAreAskedForAtTheExportsResolution()
    {
        List<ImageRequest> requests = [];
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(72, 72);
            section.Body().Image(request => { requests.Add(request); return TestImages.Png(request.PixelWidth, request.PixelHeight); });
        }));

        document.ExportSvg(new SvgExportOptions { ImageResolution = 100 });

        Assert.Equal(100, Assert.Single(requests).PixelWidth);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void AResolutionIsAboveNothing(float resolution) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SvgExportOptions { ImageResolution = resolution });

    [Fact]
    public void ADocumentIsNeeded()
    {
        Assert.Throws<ArgumentNullException>(() => SvgExport.ExportSvg(null!));
        Assert.Throws<ArgumentNullException>(() => SvgExport.ExportSvg(null!, page => "x"));
        Assert.Throws<ArgumentNullException>(() => TwoPages().ExportSvg((Func<int, string>)null!));
    }
}
