namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element that reports the same plan whatever it is offered, and records every request to draw.
/// </summary>
/// <remarks>
/// Real content guards its own drawing: a fixed shape that does not fit simply paints nothing. That makes a
/// parent which wrongly draws a wrapped or exhausted child indistinguishable from one that correctly skips it.
/// Recording the call is what makes the parent's own guard observable. Reporting a fixed plan also stands in for
/// a custom element that overstates its size, which the built-in containers must tolerate.
/// </remarks>
public sealed class ScriptedBlock(Fit plan) : Block
{
    /// <summary>The space offered on each call to <see cref="Render"/>, in order.</summary>
    public List<Extent> DrawnWith { get; } = [];

    /// <summary>
    /// A child with nothing to show here: one that does not fit (<see cref="FitKind.Defer"/>) or one that has\n    /// already finished (<see cref="FitKind.Nothing"/>).
    /// </summary>
    public static ScriptedBlock WithNothingToDraw(FitKind outcome) =>
        new(outcome == FitKind.Defer ? Fit.Defer("The scripted element does not fit.") : Fit.Nothing());

    public override Fit Plan(Extent availableSpace, PlanContext context) => plan;

    public override void Render(Extent availableSpace, RenderContext context) => DrawnWith.Add(availableSpace);
}
