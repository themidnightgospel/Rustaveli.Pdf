namespace Rustaveli.Pdf.UnitTests;

public class FlipTests
{
    [Fact]
    public void FlippingDoesNotChangeTheReportedSize()
    {
        FlipElement element = new FlipElement { FlipHorizontal = true, Child = new FixedElement(50, 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Approximately.Equal(new Size(50, 20), plan.Size);
    }

    [Fact]
    public void HorizontalFlipMirrorsContentBackOverItsOwnBox()
    {
        FlipElement element = new FlipElement { FlipHorizontal = true, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        RectangleOperation drawn = Assert.Single(page.Operations.OfType<RectangleOperation>());

        // Reflected about the box's right edge, the origin lands where the far corner was.
        Approximately.Equal(50f, drawn.Position.X);
    }

    [Fact]
    public void VerticalFlipMirrorsDownwards()
    {
        FlipElement element = new FlipElement { FlipVertical = true, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        RectangleOperation drawn = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(20f, drawn.Position.Y);
    }
}
