namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element made of equally sized units that renders as many as fit and continues on the next page.
/// </summary>
/// <remarks>
/// Stands in for genuinely splittable content such as a long paragraph, letting pagination be tested without
/// depending on text measurement.
/// </remarks>
public sealed class SplittableElement(int unitCount, float unitHeight, float width = 10f) : Element
{
    private int _rendered;

    /// <summary>Units still to be drawn.</summary>
    public int Remaining => unitCount - _rendered;

    protected override void ResetOwnState() => _rendered = 0;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        if (_rendered >= unitCount)
            return SpacePlan.Empty();

        int fitting = FittingUnits(availableSpace.Height);

        if (fitting == 0)
            return SpacePlan.Wrap($"A single unit needs {unitHeight} but only {availableSpace.Height} is available.");

        Size size = new Size(width, fitting * unitHeight);

        return _rendered + fitting >= unitCount
            ? SpacePlan.FullRender(size)
            : SpacePlan.PartialRender(size);
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        int fitting = FittingUnits(availableSpace.Height);

        for (int index = 0; index < fitting; index++)
        {
            context.Canvas.DrawRectangle(
                new Position(0, index * unitHeight),
                new Size(width, unitHeight),
                TestInks.Blue);
        }

        _rendered += fitting;
    }

    private int FittingUnits(float availableHeight)
    {
        int possible = (int)Math.Floor((availableHeight + Size.Epsilon) / unitHeight);
        return Math.Clamp(possible, 0, Remaining);
    }
}
