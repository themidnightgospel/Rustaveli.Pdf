namespace Rustaveli.Pdf.UnitTests;

public class MirrorRegressionTests
{
    [Fact]
    public void MirroredTextStaysInsideItsBox()
    {
        // The child is mirrored about the box it is actually drawn into. Mirroring about a smaller reported size
        // while drawing into a larger one throws self-aligning content off the page.
        MirrorBlock element = new MirrorBlock { Horizontally = true };
        TextBlock text = new TextBlock { Alignment = LineAlignment.Right };
        text.Runs.Add(new Text.TextRun { Text = "hello" });
        element.Child = text;

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));
        TextOperation drawn = Assert.Single(page.Texts);

        Assert.True(drawn.Position.X >= 0, $"Mirrored text was drawn at x={drawn.Position.X:F2}, outside its box.");
    }

    [Fact]
    public void MirroredRightToLeftTextStaysInsideItsBox()
    {
        MirrorBlock element = new MirrorBlock { Horizontally = true };
        TextBlock text = new TextBlock();
        text.Runs.Add(new Text.TextRun { Text = "hello" });
        element.Child = text;

        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200), context);
        TextOperation drawn = Assert.Single(page.Texts);

        Assert.True(drawn.Position.X >= 0, $"Mirrored RTL text was drawn at x={drawn.Position.X:F2}, outside its box.");
    }
}
