using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.UnitTests;

public class CountingPageSinkTests
{
    [Fact]
    public void StartsWithNoPages()
    {
        using CountingPageSink canvas = new CountingPageSink();

        Assert.Equal(0, canvas.PageCount);
    }

    [Fact]
    public void CountsEveryPageBegun()
    {
        using CountingPageSink canvas = new CountingPageSink();

        for (int page = 0; page < 3; page++)
        {
            canvas.BeginPage(new Extent(100, 100));
            canvas.EndPage();
        }

        Assert.Equal(3, canvas.PageCount);
    }

    [Fact]
    public void AcceptsEveryDrawingOperationWithoutAffectingTheCount()
    {
        // The counting pass runs the real layout against this canvas, so every operation an element can issue
        // must be accepted and discarded.
        using CountingPageSink canvas = new CountingPageSink();
        canvas.BeginPage(new Extent(100, 100));

        canvas.Save();
        canvas.Translate(new Offset(5, 5));
        canvas.Scale(2, 2);
        canvas.Rotate(90);
        canvas.ClipRectangle(new Extent(10, 10));
        canvas.DrawRectangle(Offset.Zero, new Extent(10, 10), TestInks.Red);
        canvas.DrawRoundedRectangle(Offset.Zero, new Extent(10, 10), Corners.All(2), TestInks.Red, 1);
        canvas.DrawLine(Offset.Zero, new Offset(10, 10), 1, TestInks.Red);
        canvas.DrawText("text", Offset.Zero, TypeStyle.Default);
        canvas.DrawImage(new FakeImage(10, 10), new Extent(10, 10));
        canvas.DrawExternalLink("https://example.com", new Extent(10, 10));
        canvas.DrawInternalLink("target", new Extent(10, 10));
        canvas.DrawDestination("target");
        canvas.Restore();
        canvas.EndPage();

        Assert.Equal(1, canvas.PageCount);
    }
}
