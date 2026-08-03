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
}
