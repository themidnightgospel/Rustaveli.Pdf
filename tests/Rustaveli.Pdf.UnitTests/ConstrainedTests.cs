namespace Rustaveli.Pdf.UnitTests;

public class ConstrainedTests
{
    [Fact]
    public void PinsSizeWhenMinimumAndMaximumMatch()
    {
        ConstrainedElement element = new ConstrainedElement
        {
            MinWidth = 80,
            MaxWidth = 80,
            MinHeight = 40,
            MaxHeight = 40,
            Child = new FixedElement(10, 10)
        };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Approximately.Equal(new Extent(80, 40), plan.Size);
    }

    [Fact]
    public void CapsTheSpaceOfferedToTheChild()
    {
        ConstrainedElement element = new ConstrainedElement { MaxWidth = 40, Child = new FixedElement(60, 10) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsWrap);
    }

    [Fact]
    public void WrapsWhenTheMinimumExceedsTheAvailableSpace()
    {
        ConstrainedElement element = new ConstrainedElement { MinHeight = 300, Child = new FixedElement(10, 10) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsWrap);
        Assert.Contains("minimum height", plan.WrapReason);
    }

    [Fact]
    public void LeavesAnUnconstrainedAxisAtTheChildSize()
    {
        ConstrainedElement element = new ConstrainedElement { MinWidth = 100, MaxWidth = 100, Child = new FixedElement(10, 25) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Approximately.Equal(100f, plan.Size.Width);
        Approximately.Equal(25f, plan.Size.Height);
    }

    [Fact]
    public void WrapsWhenTheMinimumWidthExceedsTheAvailableWidth()
    {
        ConstrainedElement element = new ConstrainedElement { MinWidth = 250, Child = new FixedElement(10, 10) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsWrap);
        Assert.Contains("minimum width", plan.WrapReason);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChildDespiteAMinimum()
    {
        // A minimum describes the content's box; once the content is gone there is no box left to hold open.
        ConstrainedElement element = new ConstrainedElement { MinWidth = 50, MinHeight = 40, Child = new ScriptedElement(Fit.Empty()) };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 200)).IsEmpty);
    }

    [Fact]
    public void KeepsAPartialChildPartial()
    {
        ConstrainedElement element = new ConstrainedElement { MaxHeight = 70, Child = new SplittableElement(unitCount: 4, unitHeight: 30) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(new Extent(10, 60), plan.Size);
    }

    [Fact]
    public void WithoutContentOccupiesItsMinimum()
    {
        ConstrainedElement element = new ConstrainedElement { MinWidth = 50, MinHeight = 20 };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Extent(50, 20), plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 200)).Operations);
    }

    [Fact]
    public void DrawsTheChildWithinTheMaximum()
    {
        ConstrainedElement element = new ConstrainedElement { MaxWidth = 80, MaxHeight = 30, Child = new PlaceholderElement() };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Approximately.Equal(new Extent(80, 30), Assert.Single(page.Operations.OfType<RectangleOperation>()).Size);
    }
}
