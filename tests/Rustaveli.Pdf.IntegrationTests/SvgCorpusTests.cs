using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Every document of resvg's SVG test suite read into artwork and drawn on a page, as a PDF and as an image. The
/// reader refuses a document only in the way it says it does, and whatever it reads, it draws.
/// </summary>
public class SvgCorpusTests
{
    private static readonly Extent Trim = new Extent(200, 200);

    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(1);

    /// <summary>Documents with a frame that rightly draw nothing at all, as resvg draws them.</summary>
    private static readonly HashSet<string> Blank = new HashSet<string>(StringComparer.Ordinal)
    {
        // Hidden as a whole.
        "painting/display/none-on-svg.svg",

        // The slice shows the bottom half of the view box, and the picture and its frame are in the top half.
        "structure/svg/preserveAspectRatio=xMaxYMax-slice.svg",
    };

    public static TheoryData<string> Documents => SvgCorpus.Documents;

    [Theory]
    [MemberData(nameof(Documents))]
    public async Task EveryDocumentIsReadAndDrawnOnOnePage(string document)
    {
        Task<(Artwork Artwork, byte[] Pdf, IReadOnlyList<byte[]> Images)?> drawing = Task.Run(() => Draw(document));

        // A document that sends the reader round in circles fails here rather than holding up the run.
        Assert.Same(drawing, await Task.WhenAny(drawing, Task.Delay(Patience)));

        if (await drawing is not { } drawn)
            return;

        if (SvgCorpus.PlainSize(document) is { } size)
            Assert.Equal(size, drawn.Artwork.Size);

        // Most documents outline their picture with a rectangle, which is drawn whatever else is left out.
        bool framed = File.ReadAllText(SvgCorpus.PathOf(document)).Contains("id=\"frame\"") && !Blank.Contains(document);

        using (UglyToad.PdfPig.PdfDocument read = UglyToad.PdfPig.PdfDocument.Open(drawn.Pdf))
        {
            Assert.Equal(1, read.NumberOfPages);
            Assert.Equal((200d, 200d), (read.GetPage(1).Width, read.GetPage(1).Height));
            Assert.True(!framed || read.GetPage(1).Paths.Count > 0, $"{document} draws no frame in the PDF.");
        }

        using SKBitmap image = SKBitmap.Decode(Assert.Single(drawn.Images));
        SKColor[] pixels = image.Pixels;
        Assert.Equal((200, 200), (image.Width, image.Height));
        Assert.True(!framed || pixels.Any(pixel => pixel != pixels[0]), $"{document} draws no frame in the image.");
    }

    /// <summary>
    /// The artwork a document reads into, and the page it is placed on as a PDF and an image; nothing for a document
    /// the reader may refuse, and refuses as it documents, with a <see cref="FormatException"/>.
    /// </summary>
    private static (Artwork Artwork, byte[] Pdf, IReadOnlyList<byte[]> Images)? Draw(string document)
    {
        Artwork artwork;

        try
        {
            artwork = Artwork.FromSvgFile(SvgCorpus.PathOf(document));
        }
        catch (FormatException) when (SvgCorpus.Refusable.Contains(document))
        {
            return null;
        }

        Document page = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = Trim;
            section.Margins = Sides.All(0);
            section.Body().Artwork(artwork, ImageFitting.Proportionally);
        }));

        return (artwork, page.ExportPdf(), page.ExportImages(new ImageExportOptions { Resolution = 72, Format = PageImageFormat.Png }));
    }
}
