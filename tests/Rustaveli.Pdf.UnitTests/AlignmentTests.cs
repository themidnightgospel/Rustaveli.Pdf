namespace Rustaveli.Pdf.UnitTests;

public class AlignmentTests
{
    [Theory]
    [InlineData(HorizontalAlignment.Left, 0f)]
    [InlineData(HorizontalAlignment.Center, 75f)]
    [InlineData(HorizontalAlignment.Right, 150f)]
    public void PositionsTheChildHorizontally(HorizontalAlignment alignment, float expectedX)
    {
        AlignmentElement element = new AlignmentElement { Horizontal = alignment, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(expectedX, rectangle.Position.X);
    }

    [Theory]
    [InlineData(VerticalAlignment.Top, 0f)]
    [InlineData(VerticalAlignment.Middle, 40f)]
    [InlineData(VerticalAlignment.Bottom, 80f)]
    public void PositionsTheChildVertically(VerticalAlignment alignment, float expectedY)
    {
        AlignmentElement element = new AlignmentElement { Vertical = alignment, Child = new FixedElement(50, 20) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(expectedY, rectangle.Position.Y);
    }

    [Fact]
    public void ClaimsTheFullSpaceOnlyOnAlignedAxes()
    {
        AlignmentElement element = new AlignmentElement { Horizontal = HorizontalAlignment.Center, Child = new FixedElement(50, 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Approximately.Equal(200f, plan.Size.Width);
        Approximately.Equal(20f, plan.Size.Height);
    }

    [Fact]
    public void CombinesBothAxesIntoOneElement()
    {
        // Chaining must not nest two aligners, or the inner one would receive an already-collapsed box.
        Element root = LayoutHarness.Build(container => container.AlignRight().AlignMiddle().Element(inner =>
            inner.Child = new FixedElement(50, 20)));

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 100));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Position(150, 40), rectangle.Position);
    }
}
