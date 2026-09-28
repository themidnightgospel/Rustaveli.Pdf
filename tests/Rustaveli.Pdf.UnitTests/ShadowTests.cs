using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Shadows: the shape they take, the soft mask a renderer without blur paints them through, and the frames that
/// cast them.
/// </summary>
public class ShadowTests
{
    // ---- The shape -------------------------------------------------------------------------------------------

    [Fact]
    public void AShadowIsTheFrameMovedByItsOffset()
    {
        (Offset position, Extent size, Corners corners) = new Shadow(TestInks.Black, 4, new Offset(3, 5)).Shape(new Offset(10, 20), new Extent(50, 30), Corners.Zero);

        Assert.Equal(new Offset(13, 25), position);
        Assert.Equal(new Extent(50, 30), size);
        Assert.Equal(Corners.Zero, corners);
    }

    [Fact]
    public void ASpreadGrowsTheShadowAndItsRoundedCorners()
    {
        (Offset position, Extent size, Corners corners) = new Shadow(TestInks.Black, 4, Spread: 2).Shape(Offset.Zero, new Extent(50, 30), new Corners(5, 0, 5, 0));

        Assert.Equal(new Offset(-2, -2), position);
        Assert.Equal(new Extent(54, 34), size);
        Assert.Equal(new Corners(7, 0, 7, 0), corners);
    }

    [Fact]
    public void ANegativeSpreadShrinksTheShadowAndNeverTurnsACornerInsideOut()
    {
        (Offset position, Extent size, Corners corners) = new Shadow(TestInks.Black, 4, Spread: -3).Shape(Offset.Zero, new Extent(50, 30), new Corners(2, 8, 0, 0));

        Assert.Equal(new Offset(3, 3), position);
        Assert.Equal(new Extent(44, 24), size);
        Assert.Equal(new Corners(0, 5, 0, 0), corners);
    }

    [Theory]
    [InlineData(8f, 4f)]
    [InlineData(0f, 0f)]
    [InlineData(-2f, 0f)]
    public void TheBlurIsTwiceTheDeviation(float blur, float deviation) =>
        Assert.Equal(deviation, new Shadow(TestInks.Black, blur).Deviation);

    // ---- The soft mask ---------------------------------------------------------------------------------------

    [Fact]
    public void AMaskReachesThreeDeviationsBeyondItsShape()
    {
        ShadowMask mask = ShadowMask.Create(new Extent(40, 20), Corners.Zero, 2);

        // Two points' deviation is sampled at two pixels a point, with six points of margin on every side.
        Assert.Equal(2f, mask.PixelsPerPoint);
        Assert.Equal(6f, mask.Margin);
        Assert.Equal((104, 64), (mask.Width, mask.Height));
        Assert.Equal(new Extent(52, 32), mask.Size);
        Assert.Equal(mask.Width * mask.Height, mask.Coverage.Length);
    }

    [Fact]
    public void AMaskIsSolidInsideItsShapeAndClearFarOutside()
    {
        ShadowMask mask = ShadowMask.Create(new Extent(40, 20), Corners.Zero, 2);

        Assert.Equal(255, Coverage(mask, 26, 16));
        Assert.Equal(0, Coverage(mask, 0.5f, 0.5f));
        Assert.InRange(Coverage(mask, 6, 16), 110, 145);
    }

    [Fact]
    public void AMaskFadesSymmetricallyAcrossItsEdge()
    {
        ShadowMask mask = ShadowMask.Create(new Extent(40, 20), Corners.Zero, 2);

        // Pixels centred 1.75 points either side of the left edge, halfway down: as much inside the edge as is missing
        // outside it.
        Assert.InRange(Coverage(mask, 4.25f, 16) + Coverage(mask, 7.75f, 16), 253, 257);
    }

    [Fact]
    public void AWideBlurIsSampledMoreCoarsely()
    {
        Assert.Equal(0.25f, ShadowMask.Create(new Extent(40, 20), Corners.Zero, 40).PixelsPerPoint);
        Assert.Equal(0.5f, ShadowMask.Create(new Extent(40, 20), Corners.Zero, 8).PixelsPerPoint);
    }

    [Fact]
    public void AHugeShadowStaysWithinItsPixelBudget()
    {
        ShadowMask mask = ShadowMask.Create(new Extent(5000, 5000), Corners.Zero, 1);

        Assert.True(mask.Width * mask.Height <= ShadowMask.MaximumPixels * 1.01, $"{mask.Width} x {mask.Height}");
    }

    [Theory]
    [InlineData(1f, 1f, false)]
    [InlineData(3f, 3f, true)]
    [InlineData(20f, 1f, true)]
    [InlineData(39f, 1f, false)]
    [InlineData(39f, 19f, false)]
    [InlineData(1f, 19f, true)]
    [InlineData(-1f, 5f, false)]
    [InlineData(41f, 5f, false)]
    [InlineData(20f, 21f, false)]
    public void TheCornersAreCutByTheirArcs(float x, float y, bool inside) =>
        Assert.Equal(inside, ShadowMask.Inside(x, y, new Extent(40, 20), new Corners(4, 4, 8, 0)));

    [Fact]
    public void ALargeCornerIsCutAcrossTheMiddle()
    {
        // The top left radius runs past halfway along the top, and still cuts the corner there.
        Corners corners = new Corners(30, 10, 0, 0).FittedTo(new Extent(40, 100));

        Assert.False(ShadowMask.Inside(22, 1, new Extent(40, 100), corners));
        Assert.True(ShadowMask.Inside(22, 20, new Extent(40, 100), corners));
    }

    private static int Coverage(ShadowMask mask, float x, float y)
    {
        int column = (int)(x * mask.PixelsPerPoint);
        int row = (int)(y * mask.PixelsPerPoint);
        return mask.Coverage[(row * mask.Width) + column];
    }

    // ---- Frames that cast one --------------------------------------------------------------------------------

    private static readonly Shadow Soft = new Shadow(TestInks.Black, 6, new Offset(2, 3));

    [Fact]
    public void AShadowIsCastBeneathTheFrame()
    {
        ShadowBlock element = new ShadowBlock { Shadow = Soft, Corners = Corners.All(4), Child = new FixedBlock(50, 20, TestInks.Red) };

        List<DrawOperation> operations = LayoutHarness.Draw(element, new Extent(50, 20)).Operations;

        ShadowOperation shadow = Assert.IsType<ShadowOperation>(operations[0]);
        Assert.Equal(new Bounds(0, 0, 50, 20), shadow.Bounds);
        Assert.Equal(Soft, shadow.Shadow);
        Assert.Equal(Corners.All(4), shadow.Corners);
        Assert.IsType<RectangleOperation>(operations[1]);
    }

    [Fact]
    public void AShadowTakesNoRoom() =>
        Assert.Equal(new Extent(50, 20), LayoutHarness.Measure(new ShadowBlock { Shadow = Soft, Child = new FixedBlock(50, 20) }, new Extent(200, 200)).Size);

    [Fact]
    public void ContentThatDoesNotFitCastsNoShadow()
    {
        ShadowBlock element = new ShadowBlock { Shadow = Soft, Child = new FixedBlock(500, 20) };

        Assert.Empty(LayoutHarness.Draw(element, new Extent(50, 20)).Operations);
    }

    [Fact]
    public void DropShadowCastsTheShadowGiven()
    {
        Block root = LayoutHarness.Build(frame => frame.DropShadow(TestInks.Black, 6, 2, 3, 1).RoundCorners(5).Compose(inner => { }));
        ShadowBlock shadow = Assert.IsType<ShadowBlock>(Assert.IsAssignableFrom<Layout.EnclosingBlock>(root).Child);

        Assert.Equal(new Shadow(TestInks.Black, 6, new Offset(2, 3), 1), shadow.Shadow);
        Assert.Equal(Corners.All(5), shadow.Corners);
    }

    [Theory]
    [InlineData(-1f, 0f, 0f, 0f)]
    [InlineData(float.NaN, 0f, 0f, 0f)]
    [InlineData(float.PositiveInfinity, 0f, 0f, 0f)]
    [InlineData(1f, float.NaN, 0f, 0f)]
    [InlineData(1f, 0f, float.PositiveInfinity, 0f)]
    [InlineData(1f, 0f, 0f, float.NegativeInfinity)]
    public void AShadowMustBeMeasurable(float blur, float offsetX, float offsetY, float spread) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutHarness.Build(frame => frame.DropShadow(TestInks.Black, blur, offsetX, offsetY, spread)));
}
