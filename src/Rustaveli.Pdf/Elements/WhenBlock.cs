using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Renders its child only if a condition holds, collapsing to nothing otherwise.
/// </summary>
public sealed class WhenBlock : EnclosingBlock
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
