using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Output;
using Rustaveli.Pdf.Raster;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Where each real surface says its origin is on the page: from the top left, Y down, whatever its own space.
/// </summary>
public class SurfaceOriginTests
{
    private static void Move(ISurface surface)
    {
        surface.Translate(new Offset(10, 20));
        surface.Scale(2, 2);
        surface.Translate(new Offset(5, 5));
    }

    [Fact]
    public void ThePdfSurfaceReportsItsOriginFromTheTopOfThePage()
    {
        using MemoryStream stream = new MemoryStream();
        using PdfDocumentWriter writer = new PdfDocumentWriter(stream);
        using PdfSurface surface = new PdfSurface(writer, TypefaceLibrary.Shared.Shaper);

        surface.BeginPage(new Extent(200, 300));
        Assert.Equal(Offset.Zero, surface.Origin);

        Move(surface);
        Assert.Equal(20f, surface.Origin.X, 3);
        Assert.Equal(30f, surface.Origin.Y, 3);

        surface.EndPage();
        surface.Finish();
    }

    [Theory]
    [InlineData(72f)]
    [InlineData(150f)]
    public void TheRasterSurfaceReportsItsOriginInPointsAtAnyResolution(float resolution)
    {
        using SkiaRasterSurface surface = new SkiaRasterSurface(TypefaceLibrary.Shared.Shaper, new ImageExportOptions { Resolution = resolution });

        surface.BeginPage(new Extent(200, 300));
        Assert.Equal(Offset.Zero, surface.Origin);

        Move(surface);
        Assert.Equal(20f, surface.Origin.X, 3);
        Assert.Equal(30f, surface.Origin.Y, 3);

        surface.EndPage();
    }
}
