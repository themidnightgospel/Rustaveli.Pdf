namespace Rustaveli.Pdf.UnitTests;

public class ShrinkToFitTests
{
    [Fact]
    public void LeavesContentAloneWhenItAlreadyFits()
    {
        ShrinkToFitBlock element = new ShrinkToFitBlock { Child = new FixedBlock(50, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Approximately.Equal(new Extent(50, 20), plan.Size);
    }

    [Fact]
    public void ShrinksOversizedContentIntoTheSpace()
    {
        ShrinkToFitBlock element = new ShrinkToFitBlock { Child = new FixedBlock(200, 100) };

        Fit plan = LayoutHarness.Measure(element, new Extent(100, 100));

        Assert.True(plan.IsComplete);
        Assert.True(plan.Size.FitsIn(new Extent(100, 100)), $"Scaled content {plan.Size} should fit the offered space.");
    }

    [Fact]
    public void WrapsWhenEvenTheSmallestScaleWouldNotFit()
    {
        ShrinkToFitBlock element = new ShrinkToFitBlock { MinScale = 0.9f, Child = new FixedBlock(1000, 10) };

        Assert.True(LayoutHarness.Measure(element, new Extent(100, 100)).IsDeferred);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    public void AMinimumScaleOfOneOrMoreLeavesOversizedContentToWrapOnItsOwn(float minScale)
    {
        // "Never shrink" passes the child through, so the answer is the child's own, reason and all.
        FixedBlock child = new FixedBlock(300, 50);
        ShrinkToFitBlock element = new ShrinkToFitBlock { MinScale = minScale, Child = child };
        Extent space = new Extent(200, 100);

        Assert.Equal(LayoutHarness.Measure(child, space), LayoutHarness.Measure(element, space));
    }

    [Fact]
    public void AnUndefinedMinimumScaleMeansNoLowerBound()
    {
        ShrinkToFitBlock element = new ShrinkToFitBlock { MinScale = float.NaN, Child = new FixedBlock(400, 300) };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 100)).IsComplete);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        ShrinkToFitBlock element = new ShrinkToFitBlock { Child = new ScriptedBlock(Fit.Nothing()) };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 100)).IsNothing);
    }

    [Fact]
    public void WithoutContentOccupiesNothing()
    {
        ShrinkToFitBlock element = new ShrinkToFitBlock();

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Assert.True(plan.IsComplete);
        Approximately.Equal(Extent.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 100)).Operations);
    }
}
