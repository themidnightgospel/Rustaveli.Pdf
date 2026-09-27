using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Images generated at their final size, asked for at the resolution of the export that draws them.
/// </summary>
public class GeneratedImageTests
{
    private static Document Asking(List<ImageRequest> requests) => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = new Extent(144, 72);
        section.Body().Image(request =>
        {
            requests.Add(request);
            using SKBitmap bitmap = new SKBitmap(request.PixelWidth, request.PixelHeight);
            bitmap.Erase(SKColors.Red);
            using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return png.ToArray();
        });
    }));

    [Fact]
    public void APdfAsksAtItsImageResolution()
    {
        List<ImageRequest> requests = [];

        Asking(requests).ExportPdf(new PdfExportOptions { ImageResolution = 144 });

        ImageRequest request = Assert.Single(requests);
        Assert.Equal((288, 144, 144f), (request.PixelWidth, request.PixelHeight, request.Resolution));
    }

    [Fact]
    public void APageImageAsksAtItsOwnResolutionAndShowsTheImage()
    {
        List<ImageRequest> requests = [];

        byte[] page = Asking(requests).ExportImages(new ImageExportOptions { Resolution = 72 })[0];

        Assert.Equal((144, 72, 72f), (requests[0].PixelWidth, requests[0].PixelHeight, requests[0].Resolution));
        using SKBitmap bitmap = SKBitmap.Decode(page);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(70, 30));
    }
}
