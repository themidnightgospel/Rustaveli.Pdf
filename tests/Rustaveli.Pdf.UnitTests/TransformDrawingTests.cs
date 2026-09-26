namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// What the transforming elements actually put on the page.
/// </summary>
/// <remarks>
/// These assert on <see cref="Bounds"/> rather than on an operation's position and size. A scale changes neither
/// of those — the origin can be right while the content is drawn at the wrong size, or mirrored the wrong way —
/// so a position-only assertion cannot tell a working transform from a missing one.
/// </remarks>
public class TransformDrawingTests
{
    private static Bounds OnlyRectangle(RecordedPage page) =>
        page.Operations.OfType<RectangleOperation>().Single().Bounds;

    [Fact]
    public void ScaleToFitShrinksOversizedContentToTheWidthOfItsBox()
    {
        ScaleToFitElement element = new ScaleToFitElement { Child = new FixedElement(200, 100) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Draw(element, new Size(100, 100)));

        // Bisection converges on the largest scale that fits, approaching 0.5 from below — so the 200pt child
        // lands just under 100pt. The range matters: settling for the minimum scale instead would draw it at
        // 50pt, which is still "inside the box" and would satisfy a fits-in assertion.
        Assert.Equal(0f, bounds.Left, 2);
        Assert.InRange(bounds.Width, 99f, 100f);
        Assert.InRange(bounds.Height, 49.5f, 50f);
    }

    [Fact]
    public void ScaleToFitLeavesContentThatAlreadyFitsAtFullSize()
    {
        ScaleToFitElement element = new ScaleToFitElement { Child = new FixedElement(40, 20) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Draw(element, new Size(100, 100)));

        Assert.Equal(40f, bounds.Width, 2);
        Assert.Equal(20f, bounds.Height, 2);
    }

    [Fact]
    public void AHorizontalFlipMirrorsContentAcrossItsOwnBox()
    {
        FlipElement element = new FlipElement { FlipHorizontal = true, Child = new FixedElement(50, 20) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Draw(element, new Size(50, 20)));

        // Mirrored in place: the content still occupies [0,50], not [50,100]. Asserting only the origin cannot
        // distinguish those two, which is how a missing mirror went unnoticed.
        Assert.Equal(0f, bounds.Left, 2);
        Assert.Equal(50f, bounds.Right, 2);
        Assert.Equal(20f, bounds.Height, 2);
    }

    [Fact]
    public void AVerticalFlipMirrorsContentAcrossItsOwnBox()
    {
        FlipElement element = new FlipElement { FlipVertical = true, Child = new FixedElement(50, 20) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Draw(element, new Size(50, 20)));

        Assert.Equal(0f, bounds.Top, 2);
        Assert.Equal(20f, bounds.Bottom, 2);
    }

    [Fact]
    public void ScalingHalvesTheContentItDraws()
    {
        ScaleElement element = new ScaleElement { ScaleX = 0.5f, ScaleY = 0.5f, Child = new FixedElement(40, 20) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Draw(element, new Size(100, 100)));

        Assert.Equal(20f, bounds.Width, 2);
        Assert.Equal(10f, bounds.Height, 2);
    }

    [Fact]
    public void ScalingEachAxisIndependently()
    {
        ScaleElement element = new ScaleElement { ScaleX = 2f, ScaleY = 0.5f, Child = new FixedElement(40, 20) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Draw(element, new Size(200, 200)));

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
        ScaleElement element = new ScaleElement { ScaleX = scaleX, ScaleY = scaleY, Child = new FixedElement(50, 20) };
        Size reported = LayoutHarness.Measure(element, new Size(200, 200)).Size;

        Bounds bounds = OnlyRectangle(LayoutHarness.Draw(element, reported));

        Assert.Equal(new Size(50 * Math.Abs(scaleX), 20 * Math.Abs(scaleY)), reported);
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
        RotateElement element = new RotateElement { QuarterTurns = quarterTurns, Child = new FixedElement(100, 10) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Draw(element, new Size(200, 200)));

        // A 100x10 child turned a quarter is 10 wide and 100 tall however it was turned.
        Assert.Equal(10f, bounds.Width, 2);
        Assert.Equal(100f, bounds.Height, 2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void AQuarterTurnStaysOverItsOwnBox(int quarterTurns)
    {
        RotateElement element = new RotateElement { QuarterTurns = quarterTurns, Child = new FixedElement(100, 10) };

        // Offered exactly the turned content's size, so its box is the same however it is decided.
        Bounds bounds = OnlyRectangle(LayoutHarness.Draw(element, new Size(10, 100)));

        Assert.Equal(0f, bounds.Left, 2);
        Assert.Equal(0f, bounds.Top, 2);
        Assert.Equal(10f, bounds.Right, 2);
        Assert.Equal(100f, bounds.Bottom, 2);
    }

    [Fact]
    public void NoTurnDrawsTheContentAsItIs()
    {
        RotateElement element = new RotateElement { QuarterTurns = 4, Child = new FixedElement(100, 10) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Draw(element, new Size(200, 200)));

        Assert.Equal(new Bounds(0, 0, 100, 10), bounds);
    }

    [Fact]
    public void AHalfTurnKeepsTheExtentAndStaysOverItsOwnBox()
    {
        RotateElement element = new RotateElement { QuarterTurns = 2, Child = new FixedElement(100, 10) };

        Bounds bounds = OnlyRectangle(LayoutHarness.Draw(element, new Size(100, 10)));

        Assert.Equal(100f, bounds.Width, 2);
        Assert.Equal(10f, bounds.Height, 2);
        Assert.Equal(0f, bounds.Left, 2);
        Assert.Equal(0f, bounds.Top, 2);
    }
}
