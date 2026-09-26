namespace Rustaveli.Pdf.UnitTests;

public class UnconstrainedTests
{
    [Fact]
    public void ReportsNoSizeToItsParent()
    {
        UnconstrainedElement element = new UnconstrainedElement { Child = new FixedElement(500, 500) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(50, 50));

        Approximately.Equal(Size.Zero, plan.Size);
        Assert.True(plan.IsFullRender);
    }

    [Fact]
    public void DrawsContentLargerThanTheSpaceOffered()
    {
        UnconstrainedElement element = new UnconstrainedElement { Child = new FixedElement(500, 500) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(50, 50));
        RectangleOperation drawn = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Size(500, 500), drawn.Size);
    }

    [Fact]
    public void WithoutContentOccupiesNothing()
    {
        UnconstrainedElement element = new UnconstrainedElement();

        SpacePlan plan = LayoutHarness.Measure(element, new Size(50, 50));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(Size.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Size(50, 50)).Operations);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        UnconstrainedElement element = new UnconstrainedElement { Child = new ScriptedElement(SpacePlan.Empty()) };

        Assert.True(LayoutHarness.Measure(element, new Size(50, 50)).IsEmpty);
    }

    [Fact]
    public void PassesOnContentLargerThanTheLargestPage()
    {
        // Unbounded space still stops at the largest page PDF allows; content beyond it must be reported.
        FixedElement child = new FixedElement(20_000, 10);
        UnconstrainedElement element = new UnconstrainedElement { Child = child };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(50, 50));

        Assert.Equal(LayoutHarness.Measure(child, Size.Max), plan);
    }

    [Fact]
    public void RefusesContentThatWouldSplitEvenOnTheLargestPage()
    {
        // 20,000pt of units against a 14,400pt ceiling: the remainder would have nowhere to go.
        UnconstrainedElement element = new UnconstrainedElement { Child = new SplittableElement(unitCount: 1_000, unitHeight: 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(50, 50));

        Assert.True(plan.IsWrap);
        Assert.Contains("maximum page size", plan.WrapReason);
    }

    [Theory]
    [InlineData(SpacePlanType.Wrap)]
    [InlineData(SpacePlanType.Empty)]
    public void DoesNotAskAChildWithNothingToShowToDraw(SpacePlanType outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        UnconstrainedElement element = new UnconstrainedElement { Child = child };

        LayoutHarness.Draw(element, new Size(50, 50));

        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void DrawsTheChildIntoTheSizeItMeasuredUnbounded()
    {
        ScriptedElement child = new ScriptedElement(SpacePlan.FullRender(300, 120));
        UnconstrainedElement element = new UnconstrainedElement { Child = child };

        LayoutHarness.Draw(element, new Size(50, 50));

        Approximately.Equal(new Size(300, 120), Assert.Single(child.DrawnWith));
    }
}
