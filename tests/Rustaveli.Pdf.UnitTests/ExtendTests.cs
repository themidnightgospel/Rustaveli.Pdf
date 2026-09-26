namespace Rustaveli.Pdf.UnitTests;

public class ExtendTests
{
    [Fact]
    public void ClaimsTheFullWidthWhenExtendingHorizontally()
    {
        ExtendElement element = new ExtendElement { ExtendHorizontal = true, Child = new FixedElement(10, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Approximately.Equal(new Extent(200, 20), plan.Size);
    }

    [Fact]
    public void ClaimsOnlyTheHeightWhenExtendingVertically()
    {
        ExtendElement element = new ExtendElement { ExtendVertical = true, Child = new FixedElement(10, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Approximately.Equal(new Extent(10, 100), plan.Size);
    }

    [Fact]
    public void ClaimsBothAxesWhenExtendingFully()
    {
        ExtendElement element = new ExtendElement { ExtendHorizontal = true, ExtendVertical = true, Child = new FixedElement(10, 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void WithoutContentStillClaimsTheExtendedAxis()
    {
        ExtendElement element = new ExtendElement { ExtendHorizontal = true };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Extent(200, 0), plan.Size);
    }

    [Fact]
    public void PassesTheChildsWrapThroughUnchanged()
    {
        FixedElement child = new FixedElement(300, 20);
        ExtendElement element = new ExtendElement { ExtendHorizontal = true, ExtendVertical = true, Child = child };
        Extent space = new Extent(200, 100);

        Assert.Equal(LayoutHarness.Measure(child, space), LayoutHarness.Measure(element, space));
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        // Extending a finished child would reserve a whole blank page for it.
        ExtendElement element = new ExtendElement
        {
            ExtendHorizontal = true,
            ExtendVertical = true,
            Child = new ScriptedElement(Fit.Empty())
        };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 100)).IsEmpty);
    }

    [Fact]
    public void KeepsAPartialChildPartial()
    {
        ExtendElement element = new ExtendElement { ExtendHorizontal = true, Child = new SplittableElement(unitCount: 4, unitHeight: 30) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 70));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(new Extent(200, 60), plan.Size);
    }
}
