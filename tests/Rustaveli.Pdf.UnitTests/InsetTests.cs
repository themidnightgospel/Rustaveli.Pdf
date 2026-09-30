namespace Rustaveli.Pdf.UnitTests;

public class InsetTests
{
    [Fact]
    public void AddsInsetToTheChildSize()
    {
        InsetBlock block = new InsetBlock
        {
            Inset = Sides.All(10),
            Child = new FixedBlock(50, 20)
        };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Approximately.Equal(new Extent(70, 40), plan.Size);
    }

    [Fact]
    public void ShrinksTheSpaceOfferedToTheChild()
    {
        // The child needs 100 wide; padding leaves only 80, so it cannot fit.
        InsetBlock block = new InsetBlock
        {
            Inset = Sides.All(10),
            Child = new FixedBlock(100, 20)
        };

        Fit plan = LayoutHarness.Plan(block, new Extent(100, 200));

        Assert.True(plan.IsDeferred);
    }

    [Fact]
    public void OffsetsTheChildByTheLeadingEdges()
    {
        InsetBlock block = new InsetBlock
        {
            Inset = new Sides(10, 20, 0, 0),
            Child = new FixedBlock(50, 20)
        };

        RecordedPage page = LayoutHarness.Render(block, new Extent(200, 200));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Offset(10, 20), rectangle.Position);
    }

    [Fact]
    public void DefersWhenInsetAloneExceedsTheSpace()
    {
        InsetBlock block = new InsetBlock { Inset = Sides.All(60), Child = new FixedBlock(1, 1) };

        Fit plan = LayoutHarness.Plan(block, new Extent(100, 100));

        Assert.True(plan.IsDeferred);
    }

    [Fact]
    public void DoesNotReserveSpaceForAnExhaustedChild()
    {
        // A child that plans to nothing has nothing left for a continuation page; the padding must vanish with it,
        // otherwise every later page would carry a phantom band of whitespace.
        SplittableBlock splittable = new SplittableBlock(unitCount: 1, unitHeight: 10);
        InsetBlock block = new InsetBlock { Inset = Sides.All(10), Child = splittable };
        Extent space = new Extent(200, 200);

        LayoutHarness.Render(block, space);

        Assert.True(LayoutHarness.Plan(block, space).IsNothing);
    }

    [Fact]
    public void KeepsAPartialChildPartial()
    {
        // 90pt less 20pt of padding leaves room for two of the four 30pt units.
        InsetBlock block = new InsetBlock { Inset = Sides.All(10), Child = new SplittableBlock(unitCount: 4, unitHeight: 30) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 90));

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(30, 80), plan.Size);
    }

    [Fact]
    public void WithoutContentOccupiesJustTheInset()
    {
        InsetBlock block = new InsetBlock { Inset = new Sides(10, 5, 20, 15) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(30, 20), plan.Size);
        Assert.Empty(LayoutHarness.Render(block, new Extent(200, 200)).Operations);
    }

    [Fact]
    public void DrawsTheChildIntoTheInsetSpace()
    {
        InsetBlock block = new InsetBlock { Inset = new Sides(10, 5, 20, 15), Child = new PlaceholderBlock() };

        RecordedPage page = LayoutHarness.Render(block, new Extent(200, 100));
        RectangleOperation box = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Assert.Equal(new Bounds(10, 5, 180, 85), box.Bounds);
    }

    [Fact]
    public void DrawsNothingWhenInsetAloneExceedsTheSpace()
    {
        InsetBlock block = new InsetBlock { Inset = Sides.All(60), Child = new PlaceholderBlock() };

        Assert.Empty(LayoutHarness.Render(block, new Extent(100, 100)).Operations);
    }
}
