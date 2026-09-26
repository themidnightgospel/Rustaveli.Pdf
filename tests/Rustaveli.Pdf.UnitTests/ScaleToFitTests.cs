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

    [Theory]
    [InlineData(1f)]
    [InlineData(2f)]
    public void AMinimumScaleOfOneOrMoreLeavesOversizedContentToWrapOnItsOwn(float minScale)
    {
        // "Never shrink" passes the child through, so the answer is the child's own, reason and all.
        FixedElement child = new FixedElement(300, 50);
        ScaleToFitElement element = new ScaleToFitElement { MinScale = minScale, Child = child };
        Size space = new Size(200, 100);

        Assert.Equal(LayoutHarness.Measure(child, space), LayoutHarness.Measure(element, space));
    }

    [Fact]
    public void AnUndefinedMinimumScaleMeansNoLowerBound()
    {
        ScaleToFitElement element = new ScaleToFitElement { MinScale = float.NaN, Child = new FixedElement(400, 300) };

        Assert.True(LayoutHarness.Measure(element, new Size(200, 100)).IsFullRender);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        ScaleToFitElement element = new ScaleToFitElement { Child = new ScriptedElement(SpacePlan.Empty()) };

        Assert.True(LayoutHarness.Measure(element, new Size(200, 100)).IsEmpty);
    }

    [Fact]
    public void WithoutContentOccupiesNothing()
    {
        ScaleToFitElement element = new ScaleToFitElement();

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(Size.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Size(200, 100)).Operations);
    }
}
