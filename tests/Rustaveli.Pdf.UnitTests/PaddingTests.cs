namespace Rustaveli.Pdf.UnitTests;

public class PaddingTests
{
    [Fact]
    public void AddsPaddingToTheChildSize()
    {
        PaddingElement element = new PaddingElement
        {
            Padding = Edges.All(10),
            Child = new FixedElement(50, 20)
        };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Approximately.Equal(new Size(70, 40), plan.Size);
    }

    [Fact]
    public void ShrinksTheSpaceOfferedToTheChild()
    {
        // The child needs 100 wide; padding leaves only 80, so it cannot fit.
        PaddingElement element = new PaddingElement
        {
            Padding = Edges.All(10),
            Child = new FixedElement(100, 20)
        };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(100, 200));

        Assert.True(plan.IsWrap);
    }

    [Fact]
    public void OffsetsTheChildByTheLeadingEdges()
    {
        PaddingElement element = new PaddingElement
        {
            Padding = new Edges(10, 20, 0, 0),
            Child = new FixedElement(50, 20)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Position(10, 20), rectangle.Position);
    }

    [Fact]
    public void WrapsWhenPaddingAloneExceedsTheSpace()
    {
        PaddingElement element = new PaddingElement { Padding = Edges.All(60), Child = new FixedElement(1, 1) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(100, 100));

        Assert.True(plan.IsWrap);
    }

    [Fact]
    public void DoesNotReserveSpaceForAnExhaustedChild()
    {
        // A child reporting Empty has nothing left for a continuation page; the padding must vanish with it,
        // otherwise every later page would carry a phantom band of whitespace.
        SplittableElement splittable = new SplittableElement(unitCount: 1, unitHeight: 10);
        PaddingElement element = new PaddingElement { Padding = Edges.All(10), Child = splittable };
        Size space = new Size(200, 200);

        LayoutHarness.Draw(element, space);

        Assert.True(LayoutHarness.Measure(element, space).IsEmpty);
    }

    [Fact]
    public void KeepsAPartialChildPartial()
    {
        // 90pt less 20pt of padding leaves room for two of the four 30pt units.
        PaddingElement element = new PaddingElement { Padding = Edges.All(10), Child = new SplittableElement(unitCount: 4, unitHeight: 30) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 90));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(new Size(30, 80), plan.Size);
    }

    [Fact]
    public void WithoutContentOccupiesJustThePadding()
    {
        PaddingElement element = new PaddingElement { Padding = new Edges(10, 5, 20, 15) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Size(30, 20), plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Size(200, 200)).Operations);
    }

    [Fact]
    public void DrawsTheChildIntoTheInsetSpace()
    {
        PaddingElement element = new PaddingElement { Padding = new Edges(10, 5, 20, 15), Child = new PlaceholderElement() };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 100));
        RectangleOperation block = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Assert.Equal(new Bounds(10, 5, 180, 85), block.Bounds);
    }

    [Fact]
    public void DrawsNothingWhenPaddingAloneExceedsTheSpace()
    {
        PaddingElement element = new PaddingElement { Padding = Edges.All(60), Child = new PlaceholderElement() };

        Assert.Empty(LayoutHarness.Draw(element, new Size(100, 100)).Operations);
    }
}
