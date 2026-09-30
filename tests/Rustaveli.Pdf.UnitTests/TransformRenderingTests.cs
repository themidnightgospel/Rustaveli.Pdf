namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// What the transforming blocks actually put on the page.
/// </summary>
/// <remarks>
/// These assert on <see cref="Bounds"/> rather than on an operation's position and size. A scale changes neither
/// of those — the origin can be right while the content is drawn at the wrong size, or mirrored the wrong way —
/// so a position-only assertion cannot tell a working transform from a missing one.
/// </remarks>
public class TransformRenderingTests
{
    private static Bounds OnlyRectangle(RecordedPage page) =>
        page.Operations.OfType<RectangleOperation>().Single().Bounds;

    [Fact]
    public void ShrinkToFitShrinksOversizedContentToTheWidthOfItsBox()
    {
        ShrinkToFitBlock block = new ShrinkToFitBlock { Child = new FixedBlock(200, 100) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Render(block, new Extent(100, 100)));

        // Bisection converges on the largest scale that fits, approaching 0.5 from below — so the 200pt child
        // lands just under 100pt. The range matters: settling for the minimum scale instead would draw it at
        // 50pt, which is still "inside the box" and would satisfy a fits-in assertion.
        Assert.Equal(0f, bounds.Left, 2);
        Assert.InRange(bounds.Width, 99f, 100f);
        Assert.InRange(bounds.Height, 49.5f, 50f);
    }

    [Fact]
    public void ShrinkToFitLeavesContentThatAlreadyFitsAtFullSize()
    {
        ShrinkToFitBlock block = new ShrinkToFitBlock { Child = new FixedBlock(40, 20) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Render(block, new Extent(100, 100)));

        Assert.Equal(40f, bounds.Width, 2);
        Assert.Equal(20f, bounds.Height, 2);
    }

    [Fact]
    public void AHorizontalMirrorReflectsContentAcrossItsOwnBox()
    {
        MirrorBlock block = new MirrorBlock { Horizontally = true, Child = new FixedBlock(50, 20) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Render(block, new Extent(50, 20)));

        // Mirrored in place: the content still occupies [0,50], not [50,100]. Asserting only the origin cannot
        // distinguish those two, which is how a missing mirror went unnoticed.
        Assert.Equal(0f, bounds.Left, 2);
        Assert.Equal(50f, bounds.Right, 2);
        Assert.Equal(20f, bounds.Height, 2);
    }

    [Fact]
    public void AVerticalMirrorReflectsContentAcrossItsOwnBox()
    {
        MirrorBlock block = new MirrorBlock { Vertically = true, Child = new FixedBlock(50, 20) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Render(block, new Extent(50, 20)));

        Assert.Equal(0f, bounds.Top, 2);
        Assert.Equal(20f, bounds.Bottom, 2);
    }

    [Fact]
    public void ScalingHalvesTheContentItDraws()
    {
        ScaleBlock block = new ScaleBlock { ScaleX = 0.5f, ScaleY = 0.5f, Child = new FixedBlock(40, 20) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Render(block, new Extent(100, 100)));

        Assert.Equal(20f, bounds.Width, 2);
        Assert.Equal(10f, bounds.Height, 2);
    }

    [Fact]
    public void ScalingEachAxisIndependently()
    {
        ScaleBlock block = new ScaleBlock { ScaleX = 2f, ScaleY = 0.5f, Child = new FixedBlock(40, 20) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Render(block, new Extent(200, 200)));

        Assert.Equal(80f, bounds.Width, 2);
        Assert.Equal(10f, bounds.Height, 2);
    }

    [Theory]
    [InlineData(-1f, 1f)]
    [InlineData(1f, -1f)]
    [InlineData(-2f, -0.5f)]
    public void ANegativeScaleMirrorsTheContentWithinTheBoxItReports(float scaleX, float scaleY)
    {
        // Regression: a negative factor reflected the content through the origin, so it was painted entirely
        // outside the box Measure reported — over the previous sibling, or off the page altogether.
        ScaleBlock block = new ScaleBlock { ScaleX = scaleX, ScaleY = scaleY, Child = new FixedBlock(50, 20) };
        Extent reported = LayoutHarness.Plan(block, new Extent(200, 200)).Size;

        Bounds bounds = OnlyRectangle(LayoutHarness.Render(block, reported));

        Assert.Equal(new Extent(50 * Math.Abs(scaleX), 20 * Math.Abs(scaleY)), reported);
        Assert.Equal(0f, bounds.Left, 2);
        Assert.Equal(0f, bounds.Top, 2);
        Assert.Equal(reported.Width, bounds.Right, 2);
        Assert.Equal(reported.Height, bounds.Bottom, 2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void AQuarterTurnSwapsTheContentsExtent(int quarterTurns)
    {
        TurnBlock block = new TurnBlock { QuarterTurns = quarterTurns, Child = new FixedBlock(100, 10) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Render(block, new Extent(200, 200)));

        // A 100x10 child turned a quarter is 10 wide and 100 tall however it was turned.
        Assert.Equal(10f, bounds.Width, 2);
        Assert.Equal(100f, bounds.Height, 2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void AQuarterTurnStaysOverItsOwnBox(int quarterTurns)
    {
        TurnBlock block = new TurnBlock { QuarterTurns = quarterTurns, Child = new FixedBlock(100, 10) };

        // Offered exactly the turned content's size, so its box is the same however it is decided.
        Bounds bounds = OnlyRectangle(LayoutHarness.Render(block, new Extent(10, 100)));

        Assert.Equal(0f, bounds.Left, 2);
        Assert.Equal(0f, bounds.Top, 2);
        Assert.Equal(10f, bounds.Right, 2);
        Assert.Equal(100f, bounds.Bottom, 2);
    }

    [Fact]
    public void NoTurnDrawsTheContentAsItIs()
    {
        TurnBlock block = new TurnBlock { QuarterTurns = 4, Child = new FixedBlock(100, 10) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Render(block, new Extent(200, 200)));

        Assert.Equal(new Bounds(0, 0, 100, 10), bounds);
    }

    [Fact]
    public void AHalfTurnKeepsTheExtentAndStaysOverItsOwnBox()
    {
        TurnBlock block = new TurnBlock { QuarterTurns = 2, Child = new FixedBlock(100, 10) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Render(block, new Extent(100, 10)));

        Assert.Equal(100f, bounds.Width, 2);
        Assert.Equal(10f, bounds.Height, 2);
        Assert.Equal(0f, bounds.Left, 2);
        Assert.Equal(0f, bounds.Top, 2);
    }

    [Theory]
    [InlineData(90f, 55f, -45f)]
    [InlineData(180f, 100f, 10f)]
    [InlineData(270f, 45f, 55f)]
    [InlineData(360f, 0f, 0f)]
    public void ARotationTurnsTheContentAboutTheCentreOfItsBox(float degrees, float x, float y)
    {
        RotateBlock block = new RotateBlock { Degrees = degrees, Child = new FixedBlock(100, 10) };

        RectangleOperation content = LayoutHarness.Render(block, new Extent(100, 10)).Operations.OfType<RectangleOperation>().Single();

        // The content's top-left corner, swung about the box's centre at (50, 5).
        Approximately.Equal(new Offset(x, y), content.Position);
    }

    [Fact]
    public void ANegativeAngleTurnsAnticlockwise()
    {
        RotateBlock block = new RotateBlock { Degrees = -90, Child = new FixedBlock(100, 10) };

        RectangleOperation content = LayoutHarness.Render(block, new Extent(100, 10)).Operations.OfType<RectangleOperation>().Single();

        Approximately.Equal(new Offset(45, 55), content.Position);
    }

    [Fact]
    public void AnyAngleIsHonoured()
    {
        RotateBlock block = new RotateBlock { Degrees = 45, Child = new FixedBlock(20, 20) };

        RectangleOperation content = LayoutHarness.Render(block, new Extent(20, 20)).Operations.OfType<RectangleOperation>().Single();

        // A square turned an eighth about its centre stands on its corner: the top-left corner rises to the top.
        Approximately.Equal(new Offset(10, 10 - (10 * (float)Math.Sqrt(2))), content.Position);
    }

    [Fact]
    public void ARotationLeavesLayoutAlone()
    {
        RotateBlock block = new RotateBlock { Degrees = 30, Child = new FixedBlock(100, 10) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 200));

        Approximately.Equal(new Extent(100, 10), plan.Size);
    }

    [Fact]
    public void ARotationWithNothingInsideDrawsNothing()
    {
        RotateBlock block = new RotateBlock { Degrees = 30 };

        Assert.Empty(LayoutHarness.Render(block, new Extent(100, 10)).Operations);
    }

    [Fact]
    public void ARotationDoesNotTurnWhatFollows()
    {
        StackBlock stack = new StackBlock();
        stack.Items.Add(new RotateBlock { Degrees = 90, Child = new FixedBlock(100, 10) });
        stack.Items.Add(new FixedBlock(100, 10));

        RectangleOperation after = LayoutHarness.Render(stack, new Extent(100, 100)).Operations.OfType<RectangleOperation>().Last();

        Assert.Equal(new Bounds(0, 10, 100, 20), after.Bounds);
    }
}
