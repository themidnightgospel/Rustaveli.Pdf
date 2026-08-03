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
}
