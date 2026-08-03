namespace Rustaveli.Pdf.UnitTests;

public class RuleAndPlaceholderTests
{
    [Fact]
    public void HorizontalRuleSpansTheWidthAtItsThickness()
    {
        Element root = LayoutHarness.Build(container => container.LineHorizontal(3, Colors.Red));

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
}
