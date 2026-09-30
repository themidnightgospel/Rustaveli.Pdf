namespace Rustaveli.Pdf.UnitTests;

public class ConstraintTests
{
    [Fact]
    public void PinsSizeWhenMinimumAndMaximumMatch()
    {
        ConstraintBlock block = new ConstraintBlock
        {
            MinWidth = 80,
            MaxWidth = 80,
            MinHeight = 40,
            MaxHeight = 40,
            Child = new FixedBlock(10, 10)
        };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Approximately.Equal(new Extent(80, 40), plan.Size);
    }

    [Fact]
    public void CapsTheSpaceOfferedToTheChild()
    {
        ConstraintBlock block = new ConstraintBlock { MaxWidth = 40, Child = new FixedBlock(60, 10) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Assert.True(plan.IsDeferred);
    }

    [Fact]
    public void DefersWhenTheMinimumExceedsTheAvailableSpace()
    {
        ConstraintBlock block = new ConstraintBlock { MinHeight = 300, Child = new FixedBlock(10, 10) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Assert.True(plan.IsDeferred);
        Assert.Contains("minimum height", plan.DeferReason);
    }

    [Fact]
    public void LeavesAnUnboundedAxisAtTheChildSize()
    {
        ConstraintBlock block = new ConstraintBlock { MinWidth = 100, MaxWidth = 100, Child = new FixedBlock(10, 25) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Approximately.Equal(100f, plan.Size.Width);
        Approximately.Equal(25f, plan.Size.Height);
    }

    [Fact]
    public void DefersWhenTheMinimumWidthExceedsTheAvailableWidth()
    {
        ConstraintBlock block = new ConstraintBlock { MinWidth = 250, Child = new FixedBlock(10, 10) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Assert.True(plan.IsDeferred);
        Assert.Contains("minimum width", plan.DeferReason);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChildDespiteAMinimum()
    {
        // A minimum describes the content's box; once the content is gone there is no box left to hold open.
        ConstraintBlock block = new ConstraintBlock { MinWidth = 50, MinHeight = 40, Child = new ScriptedBlock(Fit.Nothing()) };

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 200)).IsNothing);
    }

    [Fact]
    public void KeepsAPartialChildPartial()
    {
        ConstraintBlock block = new ConstraintBlock { MaxHeight = 70, Child = new SplittableBlock(unitCount: 4, unitHeight: 30) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(10, 60), plan.Size);
    }

    [Fact]
    public void WithoutContentOccupiesItsMinimum()
    {
        ConstraintBlock block = new ConstraintBlock { MinWidth = 50, MinHeight = 20 };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(50, 20), plan.Size);
        Assert.Empty(LayoutHarness.Render(block, new Extent(200, 200)).Operations);
    }

    [Fact]
    public void DrawsTheChildWithinTheMaximum()
    {
        ConstraintBlock block = new ConstraintBlock { MaxWidth = 80, MaxHeight = 30, Child = new PlaceholderBlock() };

        RecordedPage page = LayoutHarness.Render(block, new Extent(200, 200));

        Approximately.Equal(new Extent(80, 30), Assert.Single(page.Operations.OfType<RectangleOperation>()).Size);
    }
}
