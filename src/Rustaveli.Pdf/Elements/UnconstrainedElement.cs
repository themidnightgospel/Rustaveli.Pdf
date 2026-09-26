using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Measures its child against unlimited space, letting it exceed what the parent offered.
/// </summary>
/// <remarks>
/// Reports zero size to its parent, so surrounding content lays out as though nothing were here. Useful for
/// overlays and annotations that should not disturb the flow they sit in.
/// </remarks>
public sealed class UnconstrainedElement : EnclosingBlock
{
    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        Fit childPlan = Child?.Measure(Extent.Max, context) ?? Fit.FullRender(Extent.Zero);

        if (childPlan.IsEmpty)
            return Fit.Empty();

        // Even unbounded space has a ceiling — the largest page PDF allows. Content that cannot fit inside that
        // is reported rather than swallowed, otherwise Measure would promise a render that Draw silently skips.
        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsPartialRender)
            return Fit.Wrap("Unconstrained content does not fit even in the maximum page size, so the remainder would be lost.");

        return Fit.FullRender(Extent.Zero);
    }

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        Fit childPlan = Child?.Measure(Extent.Max, context.Layout) ?? Fit.FullRender(Extent.Zero);

        if (childPlan.IsWrap || childPlan.IsEmpty)
            return;

        Child?.Draw(childPlan.Size, context);
    }
}
