namespace Rustaveli.Pdf.UnitTests;

public class RuleAndPlaceholderTests
{
    [Fact]
    public void HorizontalRuleSpansTheWidthAtItsThickness()
    {
        Block root = LayoutHarness.Build(container => container.Rule(3, TestInks.Red));

        Fit plan = LayoutHarness.Measure(root, new Extent(200, 100));

        Approximately.Equal(new Extent(200, 3), plan.Size);
    }

    [Fact]
    public void VerticalRuleSpansTheHeightAtItsThickness()
    {
        Block root = LayoutHarness.Build(container => container.VerticalRule(2));

        Fit plan = LayoutHarness.Measure(root, new Extent(200, 100));

        Approximately.Equal(new Extent(2, 100), plan.Size);
    }

    [Fact]
    public void PlaceholderFillsTheSpaceOfferedToIt()
    {
        Block root = LayoutHarness.Build(container => container.Placeholder());

        RecordedPage page = LayoutHarness.Draw(root, new Extent(80, 40));
        RectangleOperation block = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Extent(80, 40), block.Size);
    }

    [Fact]
    public void PlaceholderClaimsTheSpaceOfferedToIt()
    {
        Fit plan = LayoutHarness.Measure(new PlaceholderBlock(), new Extent(80, 40));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(80, 40), plan.Size);
    }

    [Theory]
    [InlineData(5f, false)]
    [InlineData(4.9f, true)]
    public void HorizontalRuleWrapsWhenThickerThanTheSpace(float availableHeight, bool wraps)
    {
        RuleBlock element = new RuleBlock { Weight = 5 };

        Assert.Equal(wraps, LayoutHarness.Measure(element, new Extent(200, availableHeight)).IsDeferred);
    }

    [Fact]
    public void HorizontalRuleIsPaintedAcrossTheWidthInItsColour()
    {
        RuleBlock element = new RuleBlock { Weight = 3, Ink = TestInks.Red };

        RectangleOperation rule = Assert.Single(LayoutHarness.Draw(element, new Extent(200, 100)).Operations.OfType<RectangleOperation>());

        Assert.Equal(new Bounds(0, 0, 200, 3), rule.Bounds);
        Assert.Equal(TestInks.Red, rule.Ink);
    }

    [Theory]
    [InlineData(5f, false)]
    [InlineData(4.9f, true)]
    public void VerticalRuleWrapsWhenThickerThanTheSpace(float availableWidth, bool wraps)
    {
        VerticalRuleBlock element = new VerticalRuleBlock { Weight = 5 };

        Assert.Equal(wraps, LayoutHarness.Measure(element, new Extent(availableWidth, 100)).IsDeferred);
    }

    [Fact]
    public void VerticalRuleIsPaintedDownTheHeightInItsColour()
    {
        VerticalRuleBlock element = new VerticalRuleBlock { Weight = 2, Ink = TestInks.Blue };

        RectangleOperation rule = Assert.Single(LayoutHarness.Draw(element, new Extent(200, 100)).Operations.OfType<RectangleOperation>());

        Assert.Equal(new Bounds(0, 0, 2, 100), rule.Bounds);
        Assert.Equal(TestInks.Blue, rule.Ink);
    }
}
