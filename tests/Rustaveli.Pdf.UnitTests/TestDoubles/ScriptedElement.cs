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
public sealed class ScriptedElement(SpacePlan plan) : Element
{
    /// <summary>The space offered on each call to <see cref="Draw"/>, in order.</summary>
    public List<Size> DrawnWith { get; } = [];

    /// <summary>
    /// A child with nothing to show here: one that does not fit (<see cref="SpacePlanType.Wrap"/>) or one that has
    /// already finished (<see cref="SpacePlanType.Empty"/>).
    /// </summary>
    public static ScriptedElement WithNothingToDraw(SpacePlanType outcome) =>
        new(outcome == SpacePlanType.Wrap ? SpacePlan.Wrap("The scripted element does not fit.") : SpacePlan.Empty());

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) => plan;

    public override void Draw(Size availableSpace, DrawContext context) => DrawnWith.Add(availableSpace);
}
