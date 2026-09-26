namespace Rustaveli.Pdf.UnitTests;

public class RuleAndPlaceholderTests
{
    [Fact]
    public void HorizontalRuleSpansTheWidthAtItsThickness()
    {
        Element root = LayoutHarness.Build(container => container.LineHorizontal(3, TestInks.Red));

        SpacePlan plan = LayoutHarness.Measure(root, new Size(200, 100));

        Approximately.Equal(new Size(200, 3), plan.Size);
    }

    [Fact]
    public void VerticalRuleSpansTheHeightAtItsThickness()
    {
        Element root = LayoutHarness.Build(container => container.LineVertical(2));

        SpacePlan plan = LayoutHarness.Measure(root, new Size(200, 100));

        Approximately.Equal(new Size(2, 100), plan.Size);
    }

    [Fact]
    public void PlaceholderFillsTheSpaceOfferedToIt()
    {
        Element root = LayoutHarness.Build(container => container.Placeholder());

        RecordedPage page = LayoutHarness.Draw(root, new Size(80, 40));
        RectangleOperation block = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Size(80, 40), block.Size);
    }

    [Fact]
    public void PlaceholderClaimsTheSpaceOfferedToIt()
    {
        SpacePlan plan = LayoutHarness.Measure(new PlaceholderElement(), new Size(80, 40));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Size(80, 40), plan.Size);
    }

    [Theory]
    [InlineData(5f, false)]
    [InlineData(4.9f, true)]
    public void HorizontalRuleWrapsWhenThickerThanTheSpace(float availableHeight, bool wraps)
    {
        HorizontalLineElement element = new HorizontalLineElement { Thickness = 5 };

        Assert.Equal(wraps, LayoutHarness.Measure(element, new Size(200, availableHeight)).IsWrap);
    }

    [Fact]
    public void HorizontalRuleIsPaintedAcrossTheWidthInItsColour()
    {
        HorizontalLineElement element = new HorizontalLineElement { Thickness = 3, Color = TestInks.Red };

        RectangleOperation rule = Assert.Single(LayoutHarness.Draw(element, new Size(200, 100)).Operations.OfType<RectangleOperation>());

        Assert.Equal(new Bounds(0, 0, 200, 3), rule.Bounds);
        Assert.Equal(TestInks.Red, rule.Color);
    }

    [Theory]
    [InlineData(5f, false)]
    [InlineData(4.9f, true)]
    public void VerticalRuleWrapsWhenThickerThanTheSpace(float availableWidth, bool wraps)
    {
        VerticalLineElement element = new VerticalLineElement { Thickness = 5 };

        Assert.Equal(wraps, LayoutHarness.Measure(element, new Size(availableWidth, 100)).IsWrap);
    }

    [Fact]
    public void VerticalRuleIsPaintedDownTheHeightInItsColour()
    {
        VerticalLineElement element = new VerticalLineElement { Thickness = 2, Color = TestInks.Blue };

        RectangleOperation rule = Assert.Single(LayoutHarness.Draw(element, new Size(200, 100)).Operations.OfType<RectangleOperation>());

        Assert.Equal(new Bounds(0, 0, 2, 100), rule.Bounds);
        Assert.Equal(TestInks.Blue, rule.Color);
    }
}
