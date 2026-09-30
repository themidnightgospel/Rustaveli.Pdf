using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.UnitTests;

public class CountingPageSinkTests
{
    [Fact]
    public void StartsWithNoPages()
    {
        using CountingPageSink surface = new CountingPageSink();

        Assert.Equal(0, surface.PageCount);
    }

    [Fact]
    public void CountsEveryPageBegun()
    {
        using CountingPageSink surface = new CountingPageSink();

        for (int page = 0; page < 3; page++)
        {
            surface.BeginPage(new Extent(100, 100));
            surface.EndPage();
        }

        Assert.Equal(3, surface.PageCount);
    }

    [Fact]
    public void AcceptsEveryDrawingOperationWithoutAffectingTheCount()
    {
        // The counting pass runs the real layout against this surface, so every operation a block can issue
        // must be accepted and discarded.
        using CountingPageSink surface = new CountingPageSink();
        surface.BeginPage(new Extent(100, 100));

        surface.Save();
        surface.MoveOrigin(new Offset(5, 5));
        surface.ScaleAxes(2, 2);
        surface.RotateClockwise(90);
        surface.ClipRectangle(new Extent(10, 10));
        surface.FillRectangle(Offset.Zero, new Extent(10, 10), TestInks.Red);
        surface.DrawRoundedRectangle(Offset.Zero, new Extent(10, 10), Corners.All(2), TestInks.Red, 1);
        surface.DrawLine(Offset.Zero, new Offset(10, 10), 1, TestInks.Red);
        surface.ShowText("text", Offset.Zero, TypeStyle.Default, ReadingDirection.LeftToRight);
        surface.PaintImage(new FakeImage(10, 10), new Extent(10, 10));
        surface.LinkToUrl("https://example.com", Offset.Zero, new Extent(10, 10));
        surface.LinkToDestination("target", Offset.Zero, new Extent(10, 10));
        surface.NameDestination("target", Offset.Zero);
        surface.Tag(new Rustaveli.Pdf.Tagging.StructureElement("P", null));
        surface.Tag(null);
        surface.Restore();
        surface.EndPage();

        Assert.Equal(1, surface.PageCount);
    }
}
