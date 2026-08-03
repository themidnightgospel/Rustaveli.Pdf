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

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Approximately.Equal(new Size(80, 40), plan.Size);
    }

    [Fact]
    public void CapsTheSpaceOfferedToTheChild()
    {
        ConstrainedElement element = new ConstrainedElement { MaxWidth = 40, Child = new FixedElement(60, 10) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Assert.True(plan.IsWrap);
    }

    [Fact]
    public void WrapsWhenTheMinimumExceedsTheAvailableSpace()
    {
        ConstrainedElement element = new ConstrainedElement { MinHeight = 300, Child = new FixedElement(10, 10) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Assert.True(plan.IsWrap);
        Assert.Contains("minimum height", plan.WrapReason);
    }

    [Fact]
    public void LeavesAnUnconstrainedAxisAtTheChildSize()
    {
        ConstrainedElement element = new ConstrainedElement { MinWidth = 100, MaxWidth = 100, Child = new FixedElement(10, 25) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Approximately.Equal(100f, plan.Size.Width);
        Approximately.Equal(25f, plan.Size.Height);
    }
}
