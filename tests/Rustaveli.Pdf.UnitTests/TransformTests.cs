namespace Rustaveli.Pdf.UnitTests;

public class TransformTests
{
    [Fact]
    public void ScaleMultipliesTheReportedSize()
    {
        ScaleElement element = new ScaleElement { ScaleX = 2f, ScaleY = 3f, Child = new FixedElement(10, 10) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 500));

        Approximately.Equal(new Size(20, 30), plan.Size);
    }

    [Fact]
    public void QuarterTurnSwapsTheMeasurementAxes()
    {
        RotateElement element = new RotateElement { QuarterTurns = 1, Child = new FixedElement(100, 10) };

        // The child is 100 wide, which only fits if the rotation lets it use the 200pt vertical axis.
        SpacePlan plan = LayoutHarness.Measure(element, new Size(50, 200));

        Approximately.Equal(new Size(10, 100), plan.Size);
    }

    [Fact]
    public void NormalisesTurnsIntoASingleRevolution()
    {
        RotateElement element = new RotateElement { QuarterTurns = 5 };

        Assert.Equal(1, element.QuarterTurns);
    }

    [Fact]
    public void NormalisesNegativeTurns()
    {
        RotateElement element = new RotateElement { QuarterTurns = -1 };

        Assert.Equal(3, element.QuarterTurns);
    }

    [Fact]
    public void TranslateDoesNotAffectLayout()
    {
        TranslateElement element = new TranslateElement { Offset = new Position(25, 25), Child = new FixedElement(10, 10) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Approximately.Equal(new Size(10, 10), plan.Size);
    }

    [Fact]
    public void TranslateShiftsDrawnContent()
    {
        TranslateElement element = new TranslateElement { Offset = new Position(25, 15), Child = new FixedElement(10, 10) };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 200));
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Position(25, 15), rectangle.Position);
    }

    [Fact]
    public void TranslateWithoutContentDrawsNothing()
    {
        TranslateElement element = new TranslateElement { Offset = new Position(25, 15) };

        Assert.Empty(LayoutHarness.Draw(element, new Size(200, 200)).Operations);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    public void AZeroScaleWraps(float scaleX, float scaleY)
    {
        ScaleElement element = new ScaleElement { ScaleX = scaleX, ScaleY = scaleY, Child = new FixedElement(10, 10) };

        Assert.True(LayoutHarness.Measure(element, new Size(200, 200)).IsWrap);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    public void AZeroScaleDrawsNothing(float scaleX, float scaleY)
    {
        ScaleElement element = new ScaleElement { ScaleX = scaleX, ScaleY = scaleY, Child = new PlaceholderElement() };

        Assert.Empty(LayoutHarness.Draw(element, new Size(200, 200)).Operations);
    }

    [Fact]
    public void ScaleOffersTheChildTheSpaceItWillOccupyOnceScaled()
    {
        // At half size the child can be twice as large as the space before it stops fitting.
        ScaleElement element = new ScaleElement { ScaleX = 0.5f, ScaleY = 0.25f, Child = new PlaceholderElement() };

        Bounds bounds = LayoutHarness.Draw(element, new Size(100, 100)).Operations.OfType<RectangleOperation>().Single().Bounds;

        Assert.Equal(100f, bounds.Width, 2);
        Assert.Equal(100f, bounds.Height, 2);
    }

    [Fact]
    public void ScaleWithoutContentOccupiesNothing()
    {
        ScaleElement element = new ScaleElement { ScaleX = 2f, ScaleY = 2f };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(Size.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Size(200, 200)).Operations);
    }

    [Fact]
    public void ScalePassesTheChildsWrapThroughUnchanged()
    {
        // Doubling halves the room the child has: 100pt of its own coordinates.
        FixedElement child = new FixedElement(150, 10);
        ScaleElement element = new ScaleElement { ScaleX = 2f, ScaleY = 2f, Child = child };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Assert.Equal(LayoutHarness.Measure(child, new Size(100, 100)), plan);
    }

    [Fact]
    public void ScaleReportsEmptyForAnExhaustedChild()
    {
        ScaleElement element = new ScaleElement { ScaleX = 2f, ScaleY = 2f, Child = new ScriptedElement(SpacePlan.Empty()) };

        Assert.True(LayoutHarness.Measure(element, new Size(200, 200)).IsEmpty);
    }

    [Fact]
    public void ScaleKeepsAPartialChildPartialAtTheScaledSize()
    {
        // Halved, 100pt of height holds 200pt of the child: six of its ten 30pt units.
        ScaleElement element = new ScaleElement { ScaleX = 0.5f, ScaleY = 0.5f, Child = new SplittableElement(unitCount: 10, unitHeight: 30) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 100));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(new Size(5, 90), plan.Size);
    }

    [Fact]
    public void ANegativeScaleReportsAPositiveSize()
    {
        ScaleElement element = new ScaleElement { ScaleX = -2f, ScaleY = 1f, Child = new FixedElement(10, 10) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Approximately.Equal(new Size(20, 10), plan.Size);
    }

    [Fact]
    public void AHalfTurnKeepsTheMeasurementAxes()
    {
        RotateElement element = new RotateElement { QuarterTurns = 2, Child = new FixedElement(100, 10) };

        Approximately.Equal(new Size(100, 10), LayoutHarness.Measure(element, new Size(200, 200)).Size);
        Assert.True(LayoutHarness.Measure(element, new Size(50, 200)).IsWrap);
    }

    [Fact]
    public void RotateWithoutContentOccupiesNothing()
    {
        RotateElement element = new RotateElement { QuarterTurns = 1 };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 200));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(Size.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Size(200, 200)).Operations);
    }

    [Fact]
    public void RotatePassesTheChildsWrapThroughUnchanged()
    {
        // A quarter turn offers the child the page's height as its width.
        FixedElement child = new FixedElement(300, 10);
        RotateElement element = new RotateElement { QuarterTurns = 1, Child = child };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(50, 200));

        Assert.Equal(LayoutHarness.Measure(child, new Size(200, 50)), plan);
    }

    [Fact]
    public void RotateReportsEmptyForAnExhaustedChild()
    {
        RotateElement element = new RotateElement { QuarterTurns = 1, Child = new ScriptedElement(SpacePlan.Empty()) };

        Assert.True(LayoutHarness.Measure(element, new Size(200, 200)).IsEmpty);
    }

    [Fact]
    public void RotateKeepsAPartialChildPartialWithItsAxesSwapped()
    {
        // Turned, the child's 70pt of height is the page's 70pt of width: two of its four 30pt units.
        RotateElement element = new RotateElement { QuarterTurns = 1, Child = new SplittableElement(unitCount: 4, unitHeight: 30, width: 20) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(70, 200));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(new Size(60, 20), plan.Size);
    }

    [Theory]
    [InlineData(SpacePlanType.Wrap)]
    [InlineData(SpacePlanType.Empty)]
    public void RotateDoesNotAskAChildWithNothingToShowToDraw(SpacePlanType outcome)
    {
        ScriptedElement child = ScriptedElement.WithNothingToDraw(outcome);
        RotateElement element = new RotateElement { QuarterTurns = 1, Child = child };

        LayoutHarness.Draw(element, new Size(200, 200));

        Assert.Empty(child.DrawnWith);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void AQuarterTurnDrawsTheChildWithItsAxesSwapped(int quarterTurns)
    {
        ScriptedElement child = new ScriptedElement(SpacePlan.FullRender(100, 10));
        RotateElement element = new RotateElement { QuarterTurns = quarterTurns, Child = child };

        LayoutHarness.Draw(element, new Size(50, 200));

        Approximately.Equal(new Size(200, 50), Assert.Single(child.DrawnWith));
    }
}
