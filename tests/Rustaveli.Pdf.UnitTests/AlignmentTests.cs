namespace Rustaveli.Pdf.UnitTests;

public class AlignmentTests
{
    [Theory]
    [InlineData(HorizontalPlacement.Left, 0f)]
    [InlineData(HorizontalPlacement.Center, 75f)]
    [InlineData(HorizontalPlacement.Right, 150f)]
    public void PositionsTheChildHorizontally(HorizontalPlacement alignment, float expectedX)
    {
        PlacementBlock element = new PlacementBlock { Horizontal = alignment, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(expectedX, rectangle.Position.X);
    }

    [Theory]
    [InlineData(VerticalPlacement.Top, 0f)]
    [InlineData(VerticalPlacement.Middle, 40f)]
    [InlineData(VerticalPlacement.Bottom, 80f)]
    public void PositionsTheChildVertically(VerticalPlacement alignment, float expectedY)
    {
        PlacementBlock element = new PlacementBlock { Vertical = alignment, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(expectedY, rectangle.Position.Y);
    }

    [Fact]
    public void ClaimsTheFullSpaceOnlyOnAlignedAxes()
    {
        PlacementBlock element = new PlacementBlock { Horizontal = HorizontalPlacement.Center, Child = new FixedElement(50, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Approximately.Equal(200f, plan.Size.Width);
        Approximately.Equal(20f, plan.Size.Height);
    }

    [Fact]
    public void PassesTheChildsWrapThroughUnchanged()
    {
        FixedElement child = new FixedElement(300, 20);
        PlacementBlock element = new PlacementBlock { Horizontal = HorizontalPlacement.Center, Child = child };
        Extent space = new Extent(200, 100);

        Fit plan = LayoutHarness.Measure(element, space);

        Assert.Equal(LayoutHarness.Measure(child, space), plan);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChildEvenOnAlignedAxes()
    {
        // Claiming the full space for a child with nothing left would leave a blank block on every later page.
        PlacementBlock element = new PlacementBlock
        {
            Horizontal = HorizontalPlacement.Center,
            Vertical = VerticalPlacement.Middle,
            Child = new ScriptedElement(Fit.Empty())
        };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 100)).IsEmpty);
    }

    [Fact]
    public void KeepsAPartialChildPartialAtTheAlignedSize()
    {
        PlacementBlock element = new PlacementBlock
        {
            Horizontal = HorizontalPlacement.Right,
            Child = new SplittableElement(unitCount: 4, unitHeight: 30, width: 40)
        };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 70));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(new Extent(200, 60), plan.Size);
    }

    [Theory]
    [InlineData(FitKind.Wrap)]
    [InlineData(FitKind.Empty)]
    public void DoesNotAskAChildWithNothingToShowToDraw(FitKind outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        PlacementBlock element = new PlacementBlock { Horizontal = HorizontalPlacement.Center, Child = child };

        LayoutHarness.Draw(element, new Extent(200, 100));

        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void CombinesBothAxesIntoOneElement()
    {
        // Chaining must not nest two aligners, or the inner one would receive an already-collapsed box.
        Block root = LayoutHarness.Build(container => container.FlushRight().Middle().Element(inner =>
            inner.Child = new FixedElement(50, 20)));

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Offset(150, 40), rectangle.Position);
    }
}
