namespace Rustaveli.Pdf.UnitTests;

public class ExpandTests
{
    [Fact]
    public void ClaimsTheFullWidthWhenExtendingHorizontally()
    {
        ExpandBlock element = new ExpandBlock { Horizontally = true, Child = new FixedBlock(10, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Approximately.Equal(new Extent(200, 20), plan.Size);
    }

    [Fact]
    public void ClaimsOnlyTheHeightWhenExtendingVertically()
    {
        ExpandBlock element = new ExpandBlock { Vertically = true, Child = new FixedBlock(10, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Approximately.Equal(new Extent(10, 100), plan.Size);
    }

    [Fact]
    public void ClaimsBothAxesWhenExtendingFully()
    {
        ExpandBlock element = new ExpandBlock { Horizontally = true, Vertically = true, Child = new FixedBlock(10, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void WithoutContentStillClaimsTheExtendedAxis()
    {
        ExpandBlock element = new ExpandBlock { Horizontally = true };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(200, 0), plan.Size);
    }

    [Fact]
    public void PassesTheChildsWrapThroughUnchanged()
    {
        FixedBlock child = new FixedBlock(300, 20);
        ExpandBlock element = new ExpandBlock { Horizontally = true, Vertically = true, Child = child };
        Extent space = new Extent(200, 100);

        Assert.Equal(LayoutHarness.Measure(child, space), LayoutHarness.Measure(element, space));
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        // Extending a finished child would reserve a whole blank page for it.
        ExpandBlock element = new ExpandBlock
        {
            Horizontally = true,
            Vertically = true,
            Child = new ScriptedBlock(Fit.Nothing())
        };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 100)).IsNothing);
    }

    [Fact]
    public void KeepsAPartialChildPartial()
    {
        ExpandBlock element = new ExpandBlock { Horizontally = true, Child = new SplittableBlock(unitCount: 4, unitHeight: 30) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 70));

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(200, 60), plan.Size);
    }
}
