namespace Rustaveli.Pdf.UnitTests;

public class FlipTests
{
    [Fact]
    public void FlippingDoesNotChangeTheReportedSize()
    {
        FlipElement element = new FlipElement { FlipHorizontal = true, Child = new FixedElement(50, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Approximately.Equal(new Extent(50, 20), plan.Size);
    }

    [Fact]
    public void HorizontalFlipMirrorsContentBackOverItsOwnBox()
    {
        FlipElement element = new FlipElement { FlipHorizontal = true, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));
        RectangleOperation drawn = Assert.Single(page.Operations.OfType<RectangleOperation>());

        // Reflected about the box's right edge, the origin lands where the far corner was.
        Approximately.Equal(50f, drawn.Position.X);
    }

    [Fact]
    public void VerticalFlipMirrorsDownwards()
    {
        FlipElement element = new FlipElement { FlipVertical = true, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 20));
        RectangleOperation drawn = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(20f, drawn.Position.Y);
    }

    [Fact]
    public void DrawsNothingWithoutContent()
    {
        FlipElement element = new FlipElement { FlipHorizontal = true, FlipVertical = true };

        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 200)).Operations);
    }

    [Theory]
    [InlineData(FitKind.Wrap)]
    [InlineData(FitKind.Empty)]
    public void DoesNotAskAChildWithNothingToShowToDraw(FitKind outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        FlipElement element = new FlipElement { FlipHorizontal = true, Child = child };

        LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(child.DrawnWith);
    }
}
