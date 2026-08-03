namespace Rustaveli.Pdf.UnitTests;

public class ScaleToFitTests
{
    [Fact]
    public void LeavesContentAloneWhenItAlreadyFits()
    {
        ScaleToFitElement element = new ScaleToFitElement { Child = new FixedElement(50, 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Approximately.Equal(new Size(50, 20), plan.Size);
    }

    [Fact]
    public void ShrinksOversizedContentIntoTheSpace()
    {
        ScaleToFitElement element = new ScaleToFitElement { Child = new FixedElement(200, 100) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(100, 100));

        Assert.True(plan.IsFullRender);
        Assert.True(plan.Size.FitsIn(new Size(100, 100)), $"Scaled content {plan.Size} should fit the offered space.");
    }

    [Fact]
    public void WrapsWhenEvenTheSmallestScaleWouldNotFit()
    {
        ScaleToFitElement element = new ScaleToFitElement { MinScale = 0.9f, Child = new FixedElement(1000, 10) };

        Assert.True(LayoutHarness.Measure(element, new Size(100, 100)).IsWrap);
    }
}
