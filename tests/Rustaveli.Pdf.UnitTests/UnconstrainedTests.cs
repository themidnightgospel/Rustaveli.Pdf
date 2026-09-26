namespace Rustaveli.Pdf.UnitTests;

public class UnconstrainedTests
{
    [Fact]
    public void ReportsNoSizeToItsParent()
    {
        UnboundedBlock element = new UnboundedBlock { Child = new FixedElement(500, 500) };

        Fit plan = LayoutHarness.Measure(element, new Extent(50, 50));

        Approximately.Equal(Extent.Zero, plan.Size);
        Assert.True(plan.IsFullRender);
    }

    [Fact]
    public void DrawsContentLargerThanTheSpaceOffered()
    {
        UnboundedBlock element = new UnboundedBlock { Child = new FixedElement(500, 500) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(50, 50));
        RectangleOperation drawn = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Extent(500, 500), drawn.Size);
    }

    [Fact]
    public void WithoutContentOccupiesNothing()
    {
        UnboundedBlock element = new UnboundedBlock();

        Fit plan = LayoutHarness.Measure(element, new Extent(50, 50));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(Extent.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Extent(50, 50)).Operations);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        UnboundedBlock element = new UnboundedBlock { Child = new ScriptedElement(Fit.Empty()) };

        Assert.True(LayoutHarness.Measure(element, new Extent(50, 50)).IsEmpty);
    }

    [Fact]
    public void PassesOnContentLargerThanTheLargestPage()
    {
        // Unbounded space still stops at the largest page PDF allows; content beyond it must be reported.
        FixedElement child = new FixedElement(20_000, 10);
        UnboundedBlock element = new UnboundedBlock { Child = child };

        Fit plan = LayoutHarness.Measure(element, new Extent(50, 50));

        Assert.Equal(LayoutHarness.Measure(child, Extent.Max), plan);
    }

    [Fact]
    public void RefusesContentThatWouldSplitEvenOnTheLargestPage()
    {
        // 20,000pt of units against a 14,400pt ceiling: the remainder would have nowhere to go.
        UnboundedBlock element = new UnboundedBlock { Child = new SplittableElement(unitCount: 1_000, unitHeight: 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(50, 50));

        Assert.True(plan.IsWrap);
        Assert.Contains("maximum page size", plan.WrapReason);
    }

    [Theory]
    [InlineData(FitKind.Wrap)]
    [InlineData(FitKind.Empty)]
    public void DoesNotAskAChildWithNothingToShowToDraw(FitKind outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        UnboundedBlock element = new UnboundedBlock { Child = child };

        LayoutHarness.Draw(element, new Extent(50, 50));

        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void DrawsTheChildIntoTheSizeItMeasuredUnbounded()
    {
        ScriptedElement child = new ScriptedElement(Fit.FullRender(300, 120));
        UnboundedBlock element = new UnboundedBlock { Child = child };

        LayoutHarness.Draw(element, new Extent(50, 50));

        Approximately.Equal(new Extent(300, 120), Assert.Single(child.DrawnWith));
    }
}
