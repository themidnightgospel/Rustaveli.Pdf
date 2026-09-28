namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element made of equally sized units that renders as many as fit and continues on the next page.
/// </summary>
/// <remarks>
/// Stands in for genuinely splittable content such as a long paragraph, letting pagination be tested without
/// depending on text measurement.
/// </remarks>
internal sealed class SplittableBlock(int unitCount, float unitHeight, float width = 10f) : Block
{
    private int _rendered;

    /// <summary>Units still to be drawn.</summary>
    public int Remaining => unitCount - _rendered;

    protected override void ResetOwnState() => _rendered = 0;

    protected override object? SaveOwnProgress() => _rendered;

    protected override void RestoreOwnProgress(object progress) => _rendered = (int)progress;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (_rendered >= unitCount)
            return Fit.Nothing();

        int fitting = FittingUnits(availableSpace.Height);

        if (fitting == 0)
            return Fit.Defer($"A single unit needs {unitHeight} but only {availableSpace.Height} is available.");

        Extent size = new Extent(width, fitting * unitHeight);

        return _rendered + fitting >= unitCount
            ? Fit.Complete(size)
            : Fit.Partial(size);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        int fitting = FittingUnits(availableSpace.Height);

        for (int index = 0; index < fitting; index++)
        {
            context.Surface.DrawRectangle(
                new Offset(0, index * unitHeight),
                new Extent(width, unitHeight),
                TestInks.Blue);
        }

        _rendered += fitting;
    }

    private int FittingUnits(float availableHeight)
    {
        int possible = (int)Math.Floor((availableHeight + Extent.Epsilon) / unitHeight);
        return Math.Clamp(possible, 0, Remaining);
    }
}
