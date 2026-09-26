using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Renders its child only if a condition holds, collapsing to nothing otherwise.
/// </summary>
internal sealed class WhenBlock : EnclosingBlock
{
    public bool Condition { get; set; } = true;

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        Condition ? base.Plan(availableSpace, context) : Fit.Complete(Extent.Zero);

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (Condition)
            base.Render(availableSpace, context);
    }
}
