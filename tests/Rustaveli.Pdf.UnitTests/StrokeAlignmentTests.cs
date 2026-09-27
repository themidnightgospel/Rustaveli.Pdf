namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Where a frame's stroke lies against its edge, on a 50 by 20 box with a stroke 2 wide: inside, centred on the edge,
/// or outside it.
/// </summary>
public class StrokeAlignmentTests
{
    private static List<RectangleOperation> Sides(StrokeAlignment alignment, Sides weight)
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = weight,
            Ink = TestInks.Black,
            Alignment = alignment,
            Child = new FixedBlock(50, 20, TestInks.White)
        };

        return LayoutHarness.Draw(element, new Extent(50, 20)).Operations.OfType<RectangleOperation>()
            .Where(operation => operation.Ink == TestInks.Black)
            .ToList();
    }

    [Theory]
    [InlineData(StrokeAlignment.Inside, 0f, 0f, 50f, 20f)]
    [InlineData(StrokeAlignment.Center, -1f, -1f, 52f, 22f)]
    [InlineData(StrokeAlignment.Outside, -2f, -2f, 54f, 24f)]
    public void TheStrokeLiesInsideOnOrOutsideTheEdge(StrokeAlignment alignment, float left, float top, float width, float height)
    {
        List<RectangleOperation> sides = Sides(alignment, Rustaveli.Pdf.Sides.All(2));

        // Left, top, right, bottom: each runs the stroke's whole outer length, so the corners are filled.
        Approximately.Equal(new Offset(left, top), sides[0].Position);
        Approximately.Equal(new Extent(2, height), sides[0].Size);
        Approximately.Equal(new Offset(left, top), sides[1].Position);
        Approximately.Equal(new Extent(width, 2), sides[1].Size);
        Approximately.Equal(new Offset(left + width - 2, top), sides[2].Position);
        Approximately.Equal(new Offset(left, top + height - 2), sides[3].Position);
    }

    [Fact]
    public void SidesOfDifferentWeightsAreEachAligned()
    {
        List<RectangleOperation> sides = Sides(StrokeAlignment.Outside, new Sides(4, 2, 0, 0));

        // Only the left and top are drawn, each wholly beyond its edge.
        Assert.Equal(2, sides.Count);
        Approximately.Equal(new Offset(-4, -2), sides[0].Position);
        Approximately.Equal(new Extent(4, 22), sides[0].Size);
        Approximately.Equal(new Extent(54, 2), sides[1].Size);
    }

    [Theory]
    [InlineData(StrokeAlignment.Inside, 1f, 48f, 18f, 4f)]
    [InlineData(StrokeAlignment.Center, 0f, 50f, 20f, 5f)]
    [InlineData(StrokeAlignment.Outside, -1f, 52f, 22f, 6f)]
    public void ARoundedStrokeKeepsItsRadiusOnTheSideItAlignsTo(StrokeAlignment alignment, float inset, float width, float height, float radius)
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = Rustaveli.Pdf.Sides.All(2),
            Ink = TestInks.Black,
            Corners = Corners.All(5),
            Alignment = alignment,
            Child = new FixedBlock(50, 20)
        };

        RoundedRectangleOperation outline = Assert.Single(LayoutHarness.Draw(element, new Extent(50, 20)).Operations.OfType<RoundedRectangleOperation>());

        // The outline is the stroke's centre line: half its weight in from where the stroke's edge must fall.
        Approximately.Equal(new Offset(inset, inset), outline.Position);
        Approximately.Equal(new Extent(width, height), outline.Size);
        Approximately.Equal(radius, outline.Radius);
        Approximately.Equal(2f, outline.StrokeWidth);
    }

    [Fact]
    public void ASquareCornerStaysSquareWhereverTheStrokeLies()
    {
        StrokeBlock element = new StrokeBlock
        {
            Weight = Rustaveli.Pdf.Sides.All(2),
            Ink = TestInks.Black,
            Corners = new Corners(5, 0, 5, 0),
            Alignment = StrokeAlignment.Outside,
            Child = new FixedBlock(50, 20)
        };

        RoundedRectangleOperation outline = Assert.Single(LayoutHarness.Draw(element, new Extent(50, 20)).Operations.OfType<RoundedRectangleOperation>());

        Assert.Equal(new Corners(6, 0, 6, 0), outline.Corners);
    }

    [Fact]
    public void AlignStrokeSetsTheAlignmentOfTheStrokeItFollows()
    {
        Block root = LayoutHarness.Build(frame => frame.Stroke(2).AlignStroke(StrokeAlignment.Outside).Compose(inner => { }));
        StrokeBlock stroke = Assert.IsType<StrokeBlock>(Assert.IsAssignableFrom<Layout.EnclosingBlock>(root).Child);

        Assert.Equal(StrokeAlignment.Outside, stroke.Alignment);
    }

    [Fact]
    public void AlignStrokeMustFollowAStroke()
    {
        CompositionException exception = Assert.Throws<CompositionException>(() =>
            LayoutHarness.Build(frame => frame.Inset(2).AlignStroke(StrokeAlignment.Center)));

        Assert.Equal("AlignStroke must directly follow Stroke, StrokeLeft, StrokeTop, StrokeRight or StrokeBottom.", exception.Message);
    }

    [Fact]
    public void StrokesAreAlignedInsideUnlessTold() =>
        Assert.Equal(StrokeAlignment.Inside, new StrokeBlock().Alignment);

    [Fact]
    public void RoundCornersTakesARadiusForEachCorner()
    {
        Block root = LayoutHarness.Build(frame => frame.Fill(TestInks.Red).RoundCorners(1, 2, 3, 4).Compose(inner => { }));
        FillBlock fill = Assert.IsType<FillBlock>(Assert.IsAssignableFrom<Layout.EnclosingBlock>(root).Child);

        Assert.Equal(new Corners(1, 2, 3, 4), fill.Corners);
    }

    [Fact]
    public void AFillIsDrawnWithEachCornersRadius()
    {
        FillBlock element = new FillBlock { Ink = TestInks.Red, Corners = new Corners(1, 2, 3, 4), Child = new FixedBlock(50, 20) };

        RoundedRectangleOperation shape = Assert.Single(LayoutHarness.Draw(element, new Extent(50, 20)).Operations.OfType<RoundedRectangleOperation>());

        Assert.Equal(new Corners(1, 2, 3, 4), shape.Corners);
    }
}
