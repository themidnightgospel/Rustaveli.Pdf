using UglyToad.PdfPig;
using UglyToad.PdfPig.Graphics;

namespace Rustaveli.Pdf.IntegrationTests.Output;

/// <summary>
/// Content drawn in its draw order in the file: painted after what follows it, where it was placed.
/// </summary>
public class DrawOrderOutputTests
{
    [Fact]
    public void AHigherOrderIsPaintedLastWhereItWasPlaced()
    {
        byte[] pdf = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(200, 200);
            section.Margins = Sides.All(10);
            section.Body().Stack(stack =>
            {
                stack.Add().Height(20).DrawOrder(1).Fill(Ink.Rgb(255, 0, 0)).Blank();
                stack.Add().Height(20).Fill(Ink.Rgb(0, 0, 255)).Blank();
            });
        })).ExportPdf();

        using PdfDocument parsed = PdfDocument.Open(pdf);
        IReadOnlyList<PdfPath> paths = parsed.GetPage(1).Paths;

        // The paper, then the second fill, then the first, still at the top of the body.
        Assert.Equal(3, paths.Count);
        Assert.Equal((0d, 0d, 1d), Rgb(paths[1]));
        Assert.Equal((1d, 0d, 0d), Rgb(paths[2]));
        Assert.Equal(190, paths[2].GetBoundingRectangle()!.Value.Top, 1);
    }

    private static (double Red, double Green, double Blue) Rgb(PdfPath path)
    {
        (double red, double green, double blue) = path.FillColor!.ToRGBValues();
        return (Math.Round(red, 2), Math.Round(green, 2), Math.Round(blue, 2));
    }
}
