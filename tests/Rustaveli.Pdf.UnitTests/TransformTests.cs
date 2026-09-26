namespace Rustaveli.Pdf.UnitTests;

public class TransformTests
{
    [Fact]
    public void ScaleMultipliesTheReportedSize()
    {
        ScaleBlock element = new ScaleBlock { ScaleX = 2f, ScaleY = 3f, Child = new FixedElement(10, 10) };

        Fit plan = LayoutHarness.Measure(element, new Extent(500, 500));

        Approximately.Equal(new Extent(20, 30), plan.Size);
    }

    [Fact]
    public void QuarterTurnSwapsTheMeasurementAxes()
    {
        TurnBlock element = new TurnBlock { QuarterTurns = 1, Child = new FixedElement(100, 10) };

        // The child is 100 wide, which only fits if the rotation lets it use the 200pt vertical axis.
        Fit plan = LayoutHarness.Measure(element, new Extent(50, 200));

        Approximately.Equal(new Extent(10, 100), plan.Size);
    }

    [Fact]
    public void NormalisesTurnsIntoASingleRevolution()
    {
        TurnBlock element = new TurnBlock { QuarterTurns = 5 };

        Assert.Equal(1, element.QuarterTurns);
    }

    [Fact]
    public void NormalisesNegativeTurns()
    {
        TurnBlock element = new TurnBlock { QuarterTurns = -1 };

        Assert.Equal(3, element.QuarterTurns);
    }

    [Fact]
    public void TranslateDoesNotAffectLayout()
    {
        ShiftBlock element = new ShiftBlock { Offset = new Offset(25, 25), Child = new FixedElement(10, 10) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Approximately.Equal(new Extent(10, 10), plan.Size);
    }

    [Fact]
    public void TranslateShiftsDrawnContent()
    {
        ShiftBlock element = new ShiftBlock { Offset = new Offset(25, 15), Child = new FixedElement(10, 10) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Offset(25, 15), rectangle.Position);
    }

    [Fact]
    public void TranslateWithoutContentDrawsNothing()
    {
        ShiftBlock element = new ShiftBlock { Offset = new Offset(25, 15) };

        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 200)).Operations);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    public void AZeroScaleWraps(float scaleX, float scaleY)
    {
        ScaleBlock element = new ScaleBlock { ScaleX = scaleX, ScaleY = scaleY, Child = new FixedElement(10, 10) };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 200)).IsDeferred);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    public void AZeroScaleDrawsNothing(float scaleX, float scaleY)
    {
        ScaleBlock element = new ScaleBlock { ScaleX = scaleX, ScaleY = scaleY, Child = new PlaceholderBlock() };

        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 200)).Operations);
    }

    [Fact]
    public void ScaleOffersTheChildTheSpaceItWillOccupyOnceScaled()
    {
        // At half size the child can be twice as large as the space before it stops fitting.
        ScaleBlock element = new ScaleBlock { ScaleX = 0.5f, ScaleY = 0.25f, Child = new PlaceholderBlock() };

        Bounds bounds = LayoutHarness.Draw(element, new Extent(100, 100)).Operations.OfType<RectangleOperation>().Single().Bounds;

        Assert.Equal(100f, bounds.Width, 2);
        Assert.Equal(100f, bounds.Height, 2);
    }

    [Fact]
    public void ScaleWithoutContentOccupiesNothing()
    {
        ScaleBlock element = new ScaleBlock { ScaleX = 2f, ScaleY = 2f };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsComplete);
        Approximately.Equal(Extent.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 200)).Operations);
    }

    [Fact]
    public void ScalePassesTheChildsWrapThroughUnchanged()
    {
        // Doubling halves the room the child has: 100pt of its own coordinates.
        FixedElement child = new FixedElement(150, 10);
        ScaleBlock element = new ScaleBlock { ScaleX = 2f, ScaleY = 2f, Child = child };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.Equal(LayoutHarness.Measure(child, new Extent(100, 100)), plan);
    }

    [Fact]
    public void ScaleReportsEmptyForAnExhaustedChild()
    {
        ScaleBlock element = new ScaleBlock { ScaleX = 2f, ScaleY = 2f, Child = new ScriptedElement(Fit.Nothing()) };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 200)).IsNothing);
    }

    [Fact]
    public void ScaleKeepsAPartialChildPartialAtTheScaledSize()
    {
        // Halved, 100pt of height holds 200pt of the child: six of its ten 30pt units.
        ScaleBlock element = new ScaleBlock { ScaleX = 0.5f, ScaleY = 0.5f, Child = new SplittableElement(unitCount: 10, unitHeight: 30) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 100));

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(5, 90), plan.Size);
    }

    [Fact]
    public void ANegativeScaleReportsAPositiveSize()
    {
        ScaleBlock element = new ScaleBlock { ScaleX = -2f, ScaleY = 1f, Child = new FixedElement(10, 10) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Approximately.Equal(new Extent(20, 10), plan.Size);
    }

    [Fact]
    public void AHalfTurnKeepsTheMeasurementAxes()
    {
        TurnBlock element = new TurnBlock { QuarterTurns = 2, Child = new FixedElement(100, 10) };

        Approximately.Equal(new Extent(100, 10), LayoutHarness.Measure(element, new Extent(200, 200)).Size);
        Assert.True(LayoutHarness.Measure(element, new Extent(50, 200)).IsDeferred);
    }

    [Fact]
    public void RotateWithoutContentOccupiesNothing()
    {
        TurnBlock element = new TurnBlock { QuarterTurns = 1 };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsComplete);
        Approximately.Equal(Extent.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 200)).Operations);
    }

    [Fact]
    public void RotatePassesTheChildsWrapThroughUnchanged()
    {
        // A quarter turn offers the child the page's height as its width.
        FixedElement child = new FixedElement(300, 10);
        TurnBlock element = new TurnBlock { QuarterTurns = 1, Child = child };

        Fit plan = LayoutHarness.Measure(element, new Extent(50, 200));

        Assert.Equal(LayoutHarness.Measure(child, new Extent(200, 50)), plan);
    }

    [Fact]
    public void RotateReportsEmptyForAnExhaustedChild()
    {
        TurnBlock element = new TurnBlock { QuarterTurns = 1, Child = new ScriptedElement(Fit.Nothing()) };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 200)).IsNothing);
    }

    [Fact]
    public void RotateKeepsAPartialChildPartialWithItsAxesSwapped()
    {
        // Turned, the child's 70pt of height is the page's 70pt of width: two of its four 30pt units.
        TurnBlock element = new TurnBlock { QuarterTurns = 1, Child = new SplittableElement(unitCount: 4, unitHeight: 30, width: 20) };

        Fit plan = LayoutHarness.Measure(element, new Extent(70, 200));

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(60, 20), plan.Size);
    }

    [Theory]
    [InlineData(FitKind.Defer)]
    [InlineData(FitKind.Nothing)]
    public void RotateDoesNotAskAChildWithNothingToShowToDraw(FitKind outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        TurnBlock element = new TurnBlock { QuarterTurns = 1, Child = child };

        LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(child.DrawnWith);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void AQuarterTurnDrawsTheChildWithItsAxesSwapped(int quarterTurns)
    {
        ScriptedElement child = new ScriptedElement(Fit.Complete(100, 10));
        TurnBlock element = new TurnBlock { QuarterTurns = quarterTurns, Child = child };

        LayoutHarness.Draw(element, new Extent(50, 200));

        Approximately.Equal(new Extent(200, 50), Assert.Single(child.DrawnWith));
    }
}
