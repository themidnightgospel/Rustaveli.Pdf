using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// How images supplied to the library survive the trip into the PDF.
/// </summary>
public class ImageEncodingTests
{
    /// <summary>A hard-edged two-colour image — the shape that shows JPEG damage most clearly.</summary>
    private static byte[] FlatColour(SKEncodedImageFormat format)
    {
        using SKBitmap bitmap = new SKBitmap(64, 64);

        using (SKCanvas canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using SKPaint paint = new SKPaint { Color = new SKColor(200, 40, 40), IsAntialias = false };
            canvas.DrawRect(SKRect.Create(16, 16, 32, 32), paint);
        }

        using SKData data = bitmap.Encode(format, 90);
        return data.ToArray();
    }

    private static Document DocumentWithImages(params byte[][] images) =>
        Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 400);
            section.Body().Stack(stack =>
            {
                foreach (byte[] image in images)
                    stack.Add().Height(120).Image(RasterImage.FromBytes(image), ImageFitting.Proportionally);
            });
        }));

    [Fact]
    public void AnOpaquePngIsStoredLosslesslyRatherThanAsAJpeg()
    {
        // Logos, charts and barcodes dominate the library's use cases, and JPEG damage to them is invisible to any
        // structural check. A PNG stays a Flate-compressed image.
        byte[] pdf = DocumentWithImages(FlatColour(SKEncodedImageFormat.Png)).ExportPdf();

        Assert.DoesNotContain("/DCTDecode", Encoding.Latin1.GetString(pdf), StringComparison.Ordinal);
        Assert.Contains("/FlateDecode", Encoding.Latin1.GetString(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public void AJpegIsEmbeddedByteForByte()
    {
        // Decoding and re-encoding a JPEG loses quality a second time; PDF can carry the original as it is.
        byte[] jpeg = FlatColour(SKEncodedImageFormat.Jpeg);

        using PdfDocument parsed = PdfDocument.Open(DocumentWithImages(jpeg).ExportPdf());
        IPdfImage image = Assert.Single(parsed.GetPage(1).GetImages());

        Assert.Equal(jpeg, image.RawBytes.ToArray());
    }

    [Fact]
    public void AnImageShownTwiceIsEmbeddedOnce()
    {
        // The same bytes loaded twice are still one image: it is matched by content, not by instance.
        byte[] png = FlatColour(SKEncodedImageFormat.Png);

        string pdf = Encoding.Latin1.GetString(DocumentWithImages(png, png).ExportPdf(new PdfExportOptions { Compress = false }));

        Assert.Single(Regex.Matches(pdf, @"/Subtype\s*/Image\b"));
    }
}
