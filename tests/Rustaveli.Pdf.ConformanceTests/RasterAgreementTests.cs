using Rustaveli.Pdf.ConformanceTests.Rendering;
using Rustaveli.Pdf.ConformanceTests.Specimens;
using SkiaSharp;
using Xunit.Abstractions;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>
/// Page images drawn by Skia must agree with PDFium's rendering of the PDF: two renderers that share no code, fed
/// the same layout. A glyph, image or fill that one surface puts somewhere else shows up as a region that differs.
/// </summary>
/// <remarks>
/// Anti-aliasing differs between any two rasterisers, so pixels are compared after a blur-sized tolerance and only
/// a small share of them may differ; a misplaced glyph or a missing fill changes far more.
/// </remarks>
public class RasterAgreementTests(ITestOutputHelper output)
{
    private const float DotsPerInch = 96f;
    private const int ChannelTolerance = 96;
    private const double PixelTolerance = 0.005;

    [Theory]
    [MemberData(nameof(SpecimenCatalog.Cases), MemberType = typeof(SpecimenCatalog))]
    public void PageImagesAgreeWithTheRenderedPdf(Specimen specimen)
    {
        TestFonts.EnsureRegistered();

        List<SKBitmap> rendered = PageRenderer.Render(specimen.Build().ExportPdf(), DotsPerInch);
        IReadOnlyList<byte[]> drawn = specimen.Build().ExportImages(new ImageExportOptions { Resolution = DotsPerInch });

        try
        {
            Assert.Equal(rendered.Count, drawn.Count);

            for (int index = 0; index < drawn.Count; index++)
            {
                using SKBitmap image = SKBitmap.Decode(drawn[index]);
                SKBitmap pdf = rendered[index];

                Assert.Equal((pdf.Width, pdf.Height), (image.Width, image.Height));

                double share = DifferingShare(pdf, image);
                output.WriteLine($"{specimen.Name} page {index + 1}: {share:P3} of pixels differ.");
                Assert.True(share <= PixelTolerance, $"{specimen.Name} page {index + 1}: {share:P2} of pixels differ between the page image and the rendered PDF.");
            }
        }
        finally
        {
            foreach (SKBitmap page in rendered)
                page.Dispose();
        }
    }

    private static double DifferingShare(SKBitmap first, SKBitmap second)
    {
        int differing = 0;

        for (int y = 0; y < first.Height; y++)
        {
            for (int x = 0; x < first.Width; x++)
            {
                SKColor a = Flatten(first.GetPixel(x, y));
                SKColor b = Flatten(second.GetPixel(x, y));

                if (Math.Abs(a.Red - b.Red) > ChannelTolerance || Math.Abs(a.Green - b.Green) > ChannelTolerance || Math.Abs(a.Blue - b.Blue) > ChannelTolerance)
                    differing++;
            }
        }

        return (double)differing / (first.Width * first.Height);
    }

    /// <summary>A pixel as it shows on white, so a transparent background and white paper compare equal.</summary>
    private static SKColor Flatten(SKColor pixel)
    {
        int alpha = pixel.Alpha;
        byte Over(byte channel) => (byte)(((channel * alpha) + (255 * (255 - alpha))) / 255);
        return new SKColor(Over(pixel.Red), Over(pixel.Green), Over(pixel.Blue));
    }
}
