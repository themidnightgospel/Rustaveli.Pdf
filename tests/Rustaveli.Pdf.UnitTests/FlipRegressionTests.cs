namespace Rustaveli.Pdf.UnitTests;

public class FlipRegressionTests
{
    [Fact]
    public void MirroredTextStaysInsideItsBox()
    {
        // The child is mirrored about the box it is actually drawn into. Mirroring about a smaller reported size
        // while drawing into a larger one throws self-aligning content off the page.
        FlipElement element = new FlipElement { FlipHorizontal = true };
        TextElement text = new TextElement { Alignment = HorizontalAlignment.Right };
        text.Spans.Add(new Text.TextSpan { Text = "hello" });
        element.Child = text;

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        TextOperation drawn = Assert.Single(page.Texts);

        Assert.True(drawn.Position.X >= 0, $"Mirrored text was drawn at x={drawn.Position.X:F2}, outside its box.");
    }

    [Fact]
    public void MirroredRightToLeftTextStaysInsideItsBox()
    {
        FlipElement element = new FlipElement { FlipHorizontal = true };
        TextElement text = new TextElement();
        text.Spans.Add(new Text.TextSpan { Text = "hello" });
        element.Child = text;

        LayoutContext context = LayoutHarness.Context();
        context.ContentDirection = ContentDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200), context);
        TextOperation drawn = Assert.Single(page.Texts);

        Assert.True(drawn.Position.X >= 0, $"Mirrored RTL text was drawn at x={drawn.Position.X:F2}, outside its box.");
    }
}
