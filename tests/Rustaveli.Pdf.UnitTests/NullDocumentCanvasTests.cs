using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.UnitTests;

public class NullDocumentCanvasTests
{
    [Fact]
    public void StartsWithNoPages()
    {
        using NullDocumentCanvas canvas = new NullDocumentCanvas();

        Assert.Equal(0, canvas.PageCount);
    }

    [Fact]
    public void CountsEveryPageBegun()
    {
        using NullDocumentCanvas canvas = new NullDocumentCanvas();

        for (int page = 0; page < 3; page++)
        {
            canvas.BeginPage(new Size(100, 100));
            canvas.EndPage();
        }

        Assert.Equal(3, canvas.PageCount);
    }

    [Fact]
    public void AcceptsEveryDrawingOperationWithoutAffectingTheCount()
    {
        // The counting pass runs the real layout against this canvas, so every operation an element can issue
        // must be accepted and discarded.
        using NullDocumentCanvas canvas = new NullDocumentCanvas();
        canvas.BeginPage(new Size(100, 100));

        canvas.Save();
        canvas.Translate(new Position(5, 5));
        canvas.Scale(2, 2);
        canvas.Rotate(90);
        canvas.ClipRectangle(new Size(10, 10));
        canvas.DrawRectangle(Position.Zero, new Size(10, 10), TestInks.Red);
        canvas.DrawRoundedRectangle(Position.Zero, new Size(10, 10), 2, TestInks.Red, 1);
        canvas.DrawLine(Position.Zero, new Position(10, 10), 1, TestInks.Red);
        canvas.DrawText("text", Position.Zero, TextStyle.Default);
        canvas.DrawImage(new FakeImage(10, 10), new Size(10, 10));
        canvas.DrawExternalLink("https://example.com", new Size(10, 10));
        canvas.DrawInternalLink("target", new Size(10, 10));
        canvas.DrawDestination("target");
        canvas.Restore();
        canvas.EndPage();

        Assert.Equal(1, canvas.PageCount);
    }
}
