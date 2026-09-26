namespace Rustaveli.Pdf.UnitTests;

public class ExtendTests
{
    [Fact]
    public void ClaimsTheFullWidthWhenExtendingHorizontally()
    {
        ExtendElement element = new ExtendElement { ExtendHorizontal = true, Child = new FixedElement(10, 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Approximately.Equal(new Size(200, 20), plan.Size);
    }

    [Fact]
    public void ClaimsOnlyTheHeightWhenExtendingVertically()
    {
        ExtendElement element = new ExtendElement { ExtendVertical = true, Child = new FixedElement(10, 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Approximately.Equal(new Size(10, 100), plan.Size);
    }

    [Fact]
    public void ClaimsBothAxesWhenExtendingFully()
    {
        ExtendElement element = new ExtendElement { ExtendHorizontal = true, ExtendVertical = true, Child = new FixedElement(10, 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Approximately.Equal(new Size(200, 100), plan.Size);
    }

    [Fact]
    public void WithoutContentStillClaimsTheExtendedAxis()
    {
        ExtendElement element = new ExtendElement { ExtendHorizontal = true };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Size(200, 0), plan.Size);
    }

    [Fact]
    public void PassesTheChildsWrapThroughUnchanged()
    {
        FixedElement child = new FixedElement(300, 20);
        ExtendElement element = new ExtendElement { ExtendHorizontal = true, ExtendVertical = true, Child = child };
        Size space = new Size(200, 100);

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
            Child = new ScriptedElement(SpacePlan.Empty())
        };

        Assert.True(LayoutHarness.Measure(element, new Size(200, 100)).IsEmpty);
    }

    [Fact]
    public void KeepsAPartialChildPartial()
    {
        ExtendElement element = new ExtendElement { ExtendHorizontal = true, Child = new SplittableElement(unitCount: 4, unitHeight: 30) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 70));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(new Size(200, 60), plan.Size);
    }
}
