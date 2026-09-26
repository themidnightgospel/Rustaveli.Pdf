namespace Rustaveli.Pdf.UnitTests;

public class CornerRadiusTests
{
    [Fact]
    public void BackgroundDrawsARoundedShapeWhenGivenARadius()
    {
        Block root = LayoutHarness.Build(container => container
            .Background(TestInks.Red).CornerRadius(6)
            .Element(inner => inner.Child = new FixedElement(50, 20)));

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));
        RoundedRectangleOperation rounded = Assert.Single(page.Operations.OfType<RoundedRectangleOperation>());

        Approximately.Equal(6f, rounded.CornerRadius);
        Approximately.Equal(0f, rounded.StrokeWidth);
    }

    [Fact]
    public void BackgroundStaysSquareWithoutARadius()
    {
        Block root = LayoutHarness.Build(container => container
            .Background(TestInks.Red)
            .Element(inner => inner.Child = new FixedElement(50, 20)));

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.NotEmpty(page.Operations.OfType<RectangleOperation>());
    }

    [Fact]
    public void RoundedBorderIsStrokedRatherThanFilled()
    {
        Block root = LayoutHarness.Build(container => container
            .Border(2).CornerRadius(4)
            .Element(inner => inner.Child = new FixedElement(50, 20, TestInks.White)));

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));
        RoundedRectangleOperation rounded = Assert.Single(page.Operations.OfType<RoundedRectangleOperation>());

        Approximately.Equal(2f, rounded.StrokeWidth);
    }

    [Fact]
    public void BorderWithUnevenWidthsKeepsSquareCorners()
    {
        // A rounded corner has no meaningful shape where two different thicknesses meet.
        StrokeBlock element = new StrokeBlock
        {
            Width = new Sides(1, 4, 1, 1),
            CornerRadius = 5,
            Color = TestInks.Black,
            Child = new FixedElement(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
    }

    [Fact]
    public void RejectsCornerRadiusWithoutABackgroundOrBorder()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LayoutHarness.Build(container => container.Padding(5).CornerRadius(4)));
    }
}
