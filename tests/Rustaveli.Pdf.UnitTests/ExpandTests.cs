namespace Rustaveli.Pdf.UnitTests;

public class ExpandTests
{
    [Fact]
    public void ClaimsTheFullWidthWhenExpandingHorizontally()
    {
        ExpandBlock block = new ExpandBlock { Horizontally = true, Child = new FixedBlock(10, 20) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        Approximately.Equal(new Extent(200, 20), plan.Size);
    }

    [Fact]
    public void ClaimsOnlyTheHeightWhenExpandingVertically()
    {
        ExpandBlock block = new ExpandBlock { Vertically = true, Child = new FixedBlock(10, 20) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        Approximately.Equal(new Extent(10, 100), plan.Size);
    }

    [Fact]
    public void ClaimsBothAxesWhenExpandingFully()
    {
        ExpandBlock block = new ExpandBlock { Horizontally = true, Vertically = true, Child = new FixedBlock(10, 20) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void WithoutContentStillClaimsTheExpandedAxis()
    {
        ExpandBlock block = new ExpandBlock { Horizontally = true };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 100));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(200, 0), plan.Size);
    }

    [Fact]
    public void PassesTheChildsDeferralThroughUnchanged()
    {
        FixedBlock child = new FixedBlock(300, 20);
        ExpandBlock block = new ExpandBlock { Horizontally = true, Vertically = true, Child = child };
        Extent space = new Extent(200, 100);

        Assert.Equal(LayoutHarness.Plan(child, space), LayoutHarness.Plan(block, space));
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        // Extending a finished child would reserve a whole blank page for it.
        ExpandBlock block = new ExpandBlock
        {
            Horizontally = true,
            Vertically = true,
            Child = new ScriptedBlock(Fit.Nothing())
        };

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 100)).IsNothing);
    }

    [Fact]
    public void KeepsAPartialChildPartial()
    {
        ExpandBlock block = new ExpandBlock { Horizontally = true, Child = new SplittableBlock(unitCount: 4, unitHeight: 30) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 70));

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(200, 60), plan.Size);
    }
}
