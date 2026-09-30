namespace Rustaveli.Pdf.UnitTests;

public class UnboundedTests
{
    [Fact]
    public void ReportsNoSizeToItsParent()
    {
        UnboundedBlock block = new UnboundedBlock { Child = new FixedBlock(500, 500) };

        Fit plan = LayoutHarness.Plan(block, new Extent(50, 50));

        Approximately.Equal(Extent.Zero, plan.Size);
        Assert.True(plan.IsComplete);
    }

    [Fact]
    public void DrawsContentLargerThanTheSpaceOffered()
    {
        UnboundedBlock block = new UnboundedBlock { Child = new FixedBlock(500, 500) };

        RecordedPage page = LayoutHarness.Render(block, new Extent(50, 50));
        RectangleOperation drawn = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Extent(500, 500), drawn.Size);
    }

    [Fact]
    public void WithoutContentOccupiesNothing()
    {
        UnboundedBlock block = new UnboundedBlock();

        Fit plan = LayoutHarness.Plan(block, new Extent(50, 50));

        Assert.True(plan.IsComplete);
        Approximately.Equal(Extent.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Render(block, new Extent(50, 50)).Operations);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        UnboundedBlock block = new UnboundedBlock { Child = new ScriptedBlock(Fit.Nothing()) };

        Assert.True(LayoutHarness.Plan(block, new Extent(50, 50)).IsNothing);
    }

    [Fact]
    public void PassesOnContentLargerThanTheLargestPage()
    {
        // Unbounded space still stops at the largest page PDF allows; content beyond it must be reported.
        FixedBlock child = new FixedBlock(20_000, 10);
        UnboundedBlock block = new UnboundedBlock { Child = child };

        Fit plan = LayoutHarness.Plan(block, new Extent(50, 50));

        Assert.Equal(LayoutHarness.Plan(child, Extent.Max), plan);
    }

    [Fact]
    public void RefusesContentThatWouldSplitEvenOnTheLargestPage()
    {
        // 20,000pt of units against a 14,400pt ceiling: the remainder would have nowhere to go.
        UnboundedBlock block = new UnboundedBlock { Child = new SplittableBlock(unitCount: 1_000, unitHeight: 20) };

        Fit plan = LayoutHarness.Plan(block, new Extent(50, 50));

        Assert.True(plan.IsDeferred);
        Assert.Contains("largest page", plan.DeferReason);
    }

    [Theory]
    [InlineData(nameof(FitKind.Defer))]
    [InlineData(nameof(FitKind.Nothing))]
    public void DoesNotAskAChildWithNothingToShowToDraw(string outcome)
    {
        ScriptedBlock child = ScriptedBlock.WithNothingToDraw(outcome);
        UnboundedBlock block = new UnboundedBlock { Child = child };

        LayoutHarness.Render(block, new Extent(50, 50));

        Assert.Empty(child.DrawnWith);
    }

    [Fact]
    public void DrawsTheChildIntoTheSizeItMeasuredUnbounded()
    {
        ScriptedBlock child = new ScriptedBlock(Fit.Complete(300, 120));
        UnboundedBlock block = new UnboundedBlock { Child = child };

        LayoutHarness.Render(block, new Extent(50, 50));

        Approximately.Equal(new Extent(300, 120), Assert.Single(child.DrawnWith));
    }
}
