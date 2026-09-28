using Rustaveli.Pdf.Images;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// PNGs this library writes, read back by a decoder it did not write.
/// </summary>
public class PngWriterTests
{
    [Fact]
    public void EveryPixelReadsBackAsWritten()
    {
        byte[] pixels = new byte[3 * 2 * 3];
        for (int index = 0; index < pixels.Length; index++)
            pixels[index] = (byte)(index * 13);

        using SKBitmap decoded = SKBitmap.Decode(PngWriter.Rgb(3, 2, pixels));

        Assert.Equal((3, 2), (decoded.Width, decoded.Height));

        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                int at = ((row * 3) + column) * 3;
                Assert.Equal(new SKColor(pixels[at], pixels[at + 1], pixels[at + 2]), decoded.GetPixel(column, row));
            }
        }
    }

    [Fact]
    public void PixelsMustFillTheImage() =>
        Assert.Throws<ArgumentException>(() => PngWriter.Rgb(2, 2, new byte[11]));

    [Fact]
    public void ASampleImageIsEmbeddedAndShown()
    {
        byte[] pdf = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 200);
            section.Body().Image(new SampleData(1).Image(64, 40));
        })).ExportPdf();

        using UglyToad.PdfPig.PdfDocument parsed = UglyToad.PdfPig.PdfDocument.Open(pdf);

        Assert.Single(parsed.GetPage(1).GetImages());
    }
}
