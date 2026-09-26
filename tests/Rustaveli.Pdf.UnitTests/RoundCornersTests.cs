namespace Rustaveli.Pdf.UnitTests;

public class RoundCornersTests
{
    [Fact]
    public void FillDrawsARoundedShapeWhenGivenARadius()
    {
        Block root = LayoutHarness.Build(container => container
            .Fill(TestInks.Red).RoundCorners(6)
            .Compose(inner => inner.Slot().Child = new FixedBlock(50, 20)));

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));
        RoundedRectangleOperation rounded = Assert.Single(page.Operations.OfType<RoundedRectangleOperation>());

        Approximately.Equal(6f, rounded.Radius);
        Approximately.Equal(0f, rounded.StrokeWidth);
    }

    [Fact]
    public void FillStaysSquareWithoutARadius()
    {
        Block root = LayoutHarness.Build(container => container
            .Fill(TestInks.Red)
            .Compose(inner => inner.Slot().Child = new FixedBlock(50, 20)));

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
        Assert.NotEmpty(page.Operations.OfType<RectangleOperation>());
    }

    [Fact]
    public void RoundedStrokeIsStrokedRatherThanFilled()
    {
        Block root = LayoutHarness.Build(container => container
            .Stroke(2).RoundCorners(4)
            .Compose(inner => inner.Slot().Child = new FixedBlock(50, 20, TestInks.White)));

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 200));
        RoundedRectangleOperation rounded = Assert.Single(page.Operations.OfType<RoundedRectangleOperation>());

        Approximately.Equal(2f, rounded.StrokeWidth);
    }

    [Fact]
    public void StrokeWithUnevenWidthsKeepsSquareCorners()
    {
        // A rounded corner has no meaningful shape where two different thicknesses meet.
        StrokeBlock element = new StrokeBlock
        {
            Weight = new Sides(1, 4, 1, 1),
            CornerRadius = 5,
            Ink = TestInks.Black,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations.OfType<RoundedRectangleOperation>());
    }

    [Fact]
    public void RejectsRoundCornersWithoutAFillOrStroke()
    {
        Assert.Throws<CompositionException>(() =>
            LayoutHarness.Build(container => container.Inset(5).RoundCorners(4)));
    }
}
