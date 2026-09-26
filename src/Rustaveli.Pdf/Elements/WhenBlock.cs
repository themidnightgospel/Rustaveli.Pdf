using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Renders its child only if a condition holds, collapsing to nothing otherwise.
/// </summary>
public sealed class WhenBlock : EnclosingBlock
{
    public bool Condition { get; set; } = true;

    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        Condition ? base.Measure(availableSpace, context) : Fit.FullRender(Extent.Zero);

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        if (Condition)
            base.Draw(availableSpace, context);
    }
}
