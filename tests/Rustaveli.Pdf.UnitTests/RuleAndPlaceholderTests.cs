namespace Rustaveli.Pdf.UnitTests;

public class RuleAndPlaceholderTests
{
    [Fact]
    public void HorizontalRuleSpansTheWidthAtItsThickness()
    {
        Block root = LayoutHarness.Build(frame => frame.Rule(3, TestInks.Red));

        Fit plan = LayoutHarness.Plan(root, new Extent(200, 100));

        Approximately.Equal(new Extent(200, 3), plan.Size);
    }

    [Fact]
    public void VerticalRuleSpansTheHeightAtItsThickness()
    {
        Block root = LayoutHarness.Build(frame => frame.VerticalRule(2));

        Fit plan = LayoutHarness.Plan(root, new Extent(200, 100));

        Approximately.Equal(new Extent(2, 100), plan.Size);
    }

    [Fact]
    public void PlaceholderFillsTheSpaceOfferedToIt()
    {
        Block root = LayoutHarness.Build(frame => frame.Placeholder());

        RecordedPage page = LayoutHarness.Render(root, new Extent(80, 40));
        RectangleOperation block = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Extent(80, 40), block.Size);
    }

    [Fact]
    public void PlaceholderClaimsTheSpaceOfferedToIt()
    {
        Fit plan = LayoutHarness.Plan(new PlaceholderBlock(), new Extent(80, 40));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(80, 40), plan.Size);
    }

    [Theory]
    [InlineData(5f, false)]
    [InlineData(4.9f, true)]
    public void HorizontalRuleDefersWhenThickerThanTheSpace(float availableHeight, bool wraps)
    {
        RuleBlock block = new RuleBlock { Weight = 5 };

        Assert.Equal(wraps, LayoutHarness.Plan(block, new Extent(200, availableHeight)).IsDeferred);
    }

    [Fact]
    public void HorizontalRuleIsPaintedAcrossTheWidthInItsColour()
    {
        RuleBlock block = new RuleBlock { Weight = 3, Ink = TestInks.Red };

        RectangleOperation rule = Assert.Single(LayoutHarness.Render(block, new Extent(200, 100)).Operations.OfType<RectangleOperation>());

        Assert.Equal(new Bounds(0, 0, 200, 3), rule.Bounds);
        Assert.Equal(TestInks.Red, rule.Ink);
    }

    [Theory]
    [InlineData(5f, false)]
    [InlineData(4.9f, true)]
    public void VerticalRuleDefersWhenThickerThanTheSpace(float availableWidth, bool wraps)
    {
        VerticalRuleBlock block = new VerticalRuleBlock { Weight = 5 };

        Assert.Equal(wraps, LayoutHarness.Plan(block, new Extent(availableWidth, 100)).IsDeferred);
    }

    [Fact]
    public void VerticalRuleIsPaintedDownTheHeightInItsColour()
    {
        VerticalRuleBlock block = new VerticalRuleBlock { Weight = 2, Ink = TestInks.Blue };

        RectangleOperation rule = Assert.Single(LayoutHarness.Render(block, new Extent(200, 100)).Operations.OfType<RectangleOperation>());

        Assert.Equal(new Bounds(0, 0, 2, 100), rule.Bounds);
        Assert.Equal(TestInks.Blue, rule.Ink);
    }
}
