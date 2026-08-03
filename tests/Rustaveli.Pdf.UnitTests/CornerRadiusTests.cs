namespace Rustaveli.Pdf.UnitTests;

public class CornerRadiusTests
{
    [Fact]
    public void BackgroundDrawsARoundedShapeWhenGivenARadius()
    {
        Element root = LayoutHarness.Build(container => container
            .Background(Colors.Red).CornerRadius(6)
            .Element(inner => inner.Child = new FixedElement(50, 20)));

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 200));
        RoundedRectangleOperation rounded = Assert.Single(page.Operations.OfType<RoundedRectangleOperation>());

        Approximately.Equal(6f, rounded.CornerRadius);
        Approximately.Equal(0f, rounded.StrokeWidth);
    }

    [Fact]
    public void BackgroundStaysSquareWithoutARadius()
    {
        Element root = LayoutHarness.Build(container => container
            .Background(Colors.Red)
            .Element(inner => inner.Child = new FixedElement(50, 20)));

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 200));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.NotEmpty(page.Operations.OfType<RectangleOperation>());
    }

    [Fact]
    public void RoundedBorderIsStrokedRatherThanFilled()
    {
        Element root = LayoutHarness.Build(container => container
            .Border(2).CornerRadius(4)
            .Element(inner => inner.Child = new FixedElement(50, 20, Colors.White)));

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 200));
        RoundedRectangleOperation rounded = Assert.Single(page.Operations.OfType<RoundedRectangleOperation>());

        Approximately.Equal(2f, rounded.StrokeWidth);
    }

    [Fact]
    public void BorderWithUnevenWidthsKeepsSquareCorners()
    {
        // A rounded corner has no meaningful shape where two different thicknesses meet.
        BorderElement element = new BorderElement
        {
            Width = new Edges(1, 4, 1, 1),
            CornerRadius = 5,
            Color = Colors.Black,
            Child = new FixedElement(50, 20, Colors.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
    }

    [Fact]
    public void RejectsCornerRadiusWithoutABackgroundOrBorder()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LayoutHarness.Build(container => container.Padding(5).CornerRadius(4)));
    }
}
