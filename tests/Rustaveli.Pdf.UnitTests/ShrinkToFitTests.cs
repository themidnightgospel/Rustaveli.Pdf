namespace Rustaveli.Pdf.UnitTests;

public class ShrinkToFitTests
{
    [Fact]
    public void LeavesContentAloneWhenItAlreadyFits()
    {
        ShrinkToFitBlock block = new ShrinkToFitBlock { Child = new FixedBlock(50, 20) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Approximately.Equal(new Extent(50, 20), plan.Size);
    }

    [Fact]
    public void ShrinksOversizedContentIntoTheSpace()
    {
        ShrinkToFitBlock block = new ShrinkToFitBlock { Child = new FixedBlock(200, 100) };

        Fit plan = LayoutHarness.Plan(block, new Extent(100, 100));

        Assert.True(plan.IsComplete);
        Assert.True(plan.Size.FitsIn(new Extent(100, 100)), $"Scaled content {plan.Size} should fit the offered space.");
    }

    [Fact]
    public void DefersWhenEvenTheSmallestScaleWouldNotFit()
    {
        ShrinkToFitBlock block = new ShrinkToFitBlock { MinScale = 0.9f, Child = new FixedBlock(1000, 10) };

        Assert.True(LayoutHarness.Plan(block, new Extent(100, 100)).IsDeferred);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    public void AMinimumScaleOfOneOrMoreLeavesOversizedContentToDeferOnItsOwn(float minScale)
    {
        // "Never shrink" passes the child through, so the answer is the child's own, reason and all.
        FixedBlock child = new FixedBlock(300, 50);
        ShrinkToFitBlock block = new ShrinkToFitBlock { MinScale = minScale, Child = child };
        Extent space = new Extent(200, 100);

        Assert.Equal(LayoutHarness.Plan(child, space), LayoutHarness.Plan(block, space));
    }

    [Fact]
    public void AnUndefinedMinimumScaleMeansNoLowerBound()
    {
        ShrinkToFitBlock block = new ShrinkToFitBlock { MinScale = float.NaN, Child = new FixedBlock(400, 300) };

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 100)).IsComplete);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        ShrinkToFitBlock block = new ShrinkToFitBlock { Child = new ScriptedBlock(Fit.Nothing()) };

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 100)).IsNothing);
    }

    [Fact]
    public void ContentThatNoLongerFitsWhenAskedAgainIsNotTakenForContentOfNoSize()
    {
        // It fits when the scale is chosen and not when planned at it: its answer is passed on, so the content goes
        // on to the next page instead of vanishing as a box of no size.
        ShrinkToFitBlock block = new ShrinkToFitBlock { Child = new FitsOnce() };

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 100)).IsDeferred);
    }

    /// <summary>Content that fits the first time it is planned, and never after.</summary>
    private sealed class FitsOnce : Block
    {
        private bool _planned;

        protected override Fit PlanCore(Extent availableSpace, PlanContext context)
        {
            bool first = !_planned;
            _planned = true;
            return first ? Fit.Complete(50, 20) : Fit.Defer("It no longer fits.");
        }

        protected override void RenderCore(Extent availableSpace, RenderContext context)
        {
        }
    }

    [Fact]
    public void WithoutContentOccupiesNothing()
    {
        ShrinkToFitBlock block = new ShrinkToFitBlock();

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        Assert.True(plan.IsComplete);
        Approximately.Equal(Extent.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Render(block, new Extent(200, 100)).Operations);
    }
}
