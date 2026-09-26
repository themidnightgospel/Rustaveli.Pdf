using Rustaveli.Pdf.Images;
using Rustaveli.Pdf.Output;

namespace Rustaveli.Pdf.UnitTests.Output;

/// <summary>
/// Where an image's stored corners land for each EXIF orientation. PDF draws an image into the unit square with its
/// first stored row at the top (v = 1); the placement must carry each stored corner to where the upright image has
/// it, inside a box drawn with Y running down.
/// </summary>
public class PlacementTests
{
    private const double Width = 40;
    private const double Height = 30;

    /// <summary>For each orientation, where the stored top-left, top-right and bottom-left corners appear upright.</summary>
    public static TheoryData<int, double, double, double, double, double, double> Orientations() => new()
    {
        // orientation, top-left (x, y), top-right (x, y), bottom-left (x, y), in the upright box.
        { 1, 0, 0, Width, 0, 0, Height },
        { 2, Width, 0, 0, 0, Width, Height },
        { 3, Width, Height, 0, Height, Width, 0 },
        { 4, 0, Height, Width, Height, 0, 0 },
        { 5, 0, 0, 0, Height, Width, 0 },
        { 6, Width, 0, Width, Height, 0, 0 },
        { 7, Width, Height, Width, 0, 0, Height },
        { 8, 0, Height, 0, 0, Width, Height },
    };

    [Theory]
    [MemberData(nameof(Orientations))]
    public void CarriesEachStoredCornerToItsUprightPlace(
        int orientation, double topLeftX, double topLeftY, double topRightX, double topRightY, double bottomLeftX, double bottomLeftY)
    {
        Transform placement = PdfSurface.Placement((ExifOrientation)orientation, Width, Height);

        // Stored corners in the unit square: top-left (0, 1), top-right (1, 1), bottom-left (0, 0).
        Assert.Equal((topLeftX, topLeftY), placement.Apply(0, 1));
        Assert.Equal((topRightX, topRightY), placement.Apply(1, 1));
        Assert.Equal((bottomLeftX, bottomLeftY), placement.Apply(0, 0));
    }

    [Fact]
    public void ComposesTransformsInTheOrderAContentStreamDoes()
    {
        // Translate, then scale: a point is scaled first and the result moved, as nested cm operators apply.
        Transform translated = Transform.Identity.After(new Transform(1, 0, 0, 1, 10, 20));
        Transform scaled = translated.After(new Transform(2, 0, 0, 3, 0, 0));

        Assert.Equal((12.0, 23.0), scaled.Apply(1, 1));
    }
}
