using System.Text;
using Rustaveli.Pdf.Documents;
using Rustaveli.Pdf.Fluent;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// How images supplied to the library survive the trip into the PDF.
/// </summary>
public class ImageEncodingTests
{
    /// <summary>A hard-edged two-colour image — the shape that shows JPEG damage most clearly.</summary>
    private static byte[] FlatColourPng()
    {
        using SKBitmap bitmap = new SKBitmap(64, 64);

        using (SKCanvas canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using SKPaint paint = new SKPaint { Color = new SKColor(200, 40, 40), IsAntialias = false };
            canvas.DrawRect(SKRect.Create(16, 16, 32, 32), paint);
        }

        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static Document DocumentWithImage(byte[] png) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size = new Extent(200, 200);
            page.Content().Image(SkiaImage.FromBytes(png));
        }));

    [Fact]
    public void AnOpaqueImageIsStoredLosslesslyRatherThanAsAJpeg()
    {
        // Regression: SKDocumentPdfMetadata's parameterless constructor leaves EncodingQuality at zero, which is
        // a valid JPEG quality rather than an unset marker. Every opaque non-JPEG image was re-encoded at the
        // worst quality the format allows — catastrophic for the logos, charts and barcodes that dominate the
        // library's use cases, and invisible to any structural PDF checker.
        byte[] pdf = DocumentWithImage(FlatColourPng()).GeneratePdf();

        Assert.DoesNotContain("/DCTDecode", Encoding.Latin1.GetString(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public void TheEncodingQualityCanBeLoweredDeliberately()
    {
        // The lossless default must be a choice the caller can reverse, not a hard-coded policy.
        byte[] pdf = DocumentWithImage(FlatColourPng())
            .GeneratePdf(new PdfExportOptions { EncodingQuality = 40 });

        Assert.Contains("/DCTDecode", Encoding.Latin1.GetString(pdf), StringComparison.Ordinal);
    }

}
