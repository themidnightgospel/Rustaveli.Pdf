namespace Rustaveli.Pdf.UnitTests;

public class AlignmentTests
{
    [Theory]
    [InlineData(HorizontalAlignment.Left, 0f)]
    [InlineData(HorizontalAlignment.Center, 75f)]
    [InlineData(HorizontalAlignment.Right, 150f)]
    public void PositionsTheChildHorizontally(HorizontalAlignment alignment, float expectedX)
    {
        AlignmentElement element = new AlignmentElement { Horizontal = alignment, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(expectedX, rectangle.Position.X);
    }

    [Theory]
    [InlineData(VerticalAlignment.Top, 0f)]
    [InlineData(VerticalAlignment.Middle, 40f)]
    [InlineData(VerticalAlignment.Bottom, 80f)]
    public void PositionsTheChildVertically(VerticalAlignment alignment, float expectedY)
    {
        AlignmentElement element = new AlignmentElement { Vertical = alignment, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(expectedY, rectangle.Position.Y);
    }

    [Fact]
    public void ClaimsTheFullSpaceOnlyOnAlignedAxes()
    {
        AlignmentElement element = new AlignmentElement { Horizontal = HorizontalAlignment.Center, Child = new FixedElement(50, 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Approximately.Equal(200f, plan.Size.Width);
        Approximately.Equal(20f, plan.Size.Height);
    }

    [Fact]
    public void PassesTheChildsWrapThroughUnchanged()
    {
        FixedElement child = new FixedElement(300, 20);
        AlignmentElement element = new AlignmentElement { Horizontal = HorizontalAlignment.Center, Child = child };
        Size space = new Size(200, 100);

        SpacePlan plan = LayoutHarness.Measure(element, space);

        Assert.Equal(LayoutHarness.Measure(child, space), plan);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChildEvenOnAlignedAxes()
    {
        // Claiming the full space for a child with nothing left would leave a blank block on every later page.
        AlignmentElement element = new AlignmentElement
        {
            Horizontal = HorizontalAlignment.Center,
            Vertical = VerticalAlignment.Middle,
            Child = new ScriptedElement(SpacePlan.Empty())
        };

        Assert.True(LayoutHarness.Measure(element, new Size(200, 100)).IsEmpty);
    }

    [Fact]
    public void KeepsAPartialChildPartialAtTheAlignedSize()
    {
        AlignmentElement element = new AlignmentElement
        {
            Horizontal = HorizontalAlignment.Right,
            Child = new SplittableElement(unitCount: 4, unitHeight: 30, width: 40)
        };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 70));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(new Size(200, 60), plan.Size);
    }

    [Theory]
    [InlineData(SpacePlanType.Wrap)]
    [InlineData(SpacePlanType.Empty)]
    public void DoesNotAskAChildWithNothingToShowToDraw(SpacePlanType outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        AlignmentElement element = new AlignmentElement { Horizontal = HorizontalAlignment.Center, Child = child };

        LayoutHarness.Draw(element, new Size(200, 100));

        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void CombinesBothAxesIntoOneElement()
    {
        // Chaining must not nest two aligners, or the inner one would receive an already-collapsed box.
        Element root = LayoutHarness.Build(container => container.AlignRight().AlignMiddle().Element(inner =>
            inner.Child = new FixedElement(50, 20)));

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Position(150, 40), rectangle.Position);
    }
}
