namespace Rustaveli.Pdf.UnitTests;

public class RoundCornersTests
{
    [Fact]
    public void BackgroundDrawsARoundedShapeWhenGivenARadius()
    {
        Block root = LayoutHarness.Build(container => container
            .Fill(TestInks.Red).RoundCorners(6)
            .Compose(inner => inner.Child = new FixedBlock(50, 20)));

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));
        RoundedRectangleOperation rounded = Assert.Single(page.Operations.OfType<RoundedRectangleOperation>());

        Approximately.Equal(6f, rounded.Radius);
        Approximately.Equal(0f, rounded.StrokeWidth);
    }

    [Fact]
    public void BackgroundStaysSquareWithoutARadius()
    {
        Block root = LayoutHarness.Build(container => container
            .Fill(TestInks.Red)
            .Compose(inner => inner.Child = new FixedBlock(50, 20)));

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.NotEmpty(page.Operations.OfType<RectangleOperation>());
    }

    [Fact]
    public void RoundedBorderIsStrokedRatherThanFilled()
    {
        Block root = LayoutHarness.Build(container => container
            .Stroke(2).RoundCorners(4)
            .Compose(inner => inner.Child = new FixedBlock(50, 20, TestInks.White)));

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
            Ink = TestInks.Black,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
    }

    [Fact]
    public void RejectsCornerRadiusWithoutABackgroundOrBorder()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LayoutHarness.Build(container => container.Inset(5).RoundCorners(4)));
    }
}
