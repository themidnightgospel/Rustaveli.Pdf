namespace Rustaveli.Pdf.UnitTests;

public class ExtendTests
{
    [Fact]
    public void ClaimsTheFullWidthWhenExtendingHorizontally()
    {
        ExpandBlock element = new ExpandBlock { ExtendHorizontal = true, Child = new FixedElement(10, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Approximately.Equal(new Extent(200, 20), plan.Size);
    }

    [Fact]
    public void ClaimsOnlyTheHeightWhenExtendingVertically()
    {
        ExpandBlock element = new ExpandBlock { ExtendVertical = true, Child = new FixedElement(10, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Approximately.Equal(new Extent(10, 100), plan.Size);
    }

    [Fact]
    public void ClaimsBothAxesWhenExtendingFully()
    {
        ExpandBlock element = new ExpandBlock { ExtendHorizontal = true, ExtendVertical = true, Child = new FixedElement(10, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void WithoutContentStillClaimsTheExtendedAxis()
    {
        ExpandBlock element = new ExpandBlock { ExtendHorizontal = true };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(200, 0), plan.Size);
    }

    [Fact]
    public void PassesTheChildsWrapThroughUnchanged()
    {
        FixedElement child = new FixedElement(300, 20);
        ExpandBlock element = new ExpandBlock { ExtendHorizontal = true, ExtendVertical = true, Child = child };
        Extent space = new Extent(200, 100);

        Assert.Equal(LayoutHarness.Measure(child, space), LayoutHarness.Measure(element, space));
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        // Extending a finished child would reserve a whole blank page for it.
        ExpandBlock element = new ExpandBlock
        {
            ExtendHorizontal = true,
            ExtendVertical = true,
            Child = new ScriptedElement(Fit.Nothing())
        };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 100)).IsNothing);
    }

    [Fact]
    public void KeepsAPartialChildPartial()
    {
        ExpandBlock element = new ExpandBlock { ExtendHorizontal = true, Child = new SplittableElement(unitCount: 4, unitHeight: 30) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 70));

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(200, 60), plan.Size);
    }
}
