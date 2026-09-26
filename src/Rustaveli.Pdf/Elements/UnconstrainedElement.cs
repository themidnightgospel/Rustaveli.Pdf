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
public sealed class UnconstrainedElement : ContainerElement
{
    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        SpacePlan childPlan = Child?.Measure(Size.Max, context) ?? SpacePlan.FullRender(Size.Zero);

        if (childPlan.IsEmpty)
            return SpacePlan.Empty();

        // Even unbounded space has a ceiling — the largest page PDF allows. Content that cannot fit inside that
        // is reported rather than swallowed, otherwise Measure would promise a render that Draw silently skips.
        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsPartialRender)
            return SpacePlan.Wrap("Unconstrained content does not fit even in the maximum page size, so the remainder would be lost.");

        return SpacePlan.FullRender(Size.Zero);
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        SpacePlan childPlan = Child?.Measure(Size.Max, context.Layout) ?? SpacePlan.FullRender(Size.Zero);

        if (childPlan.IsWrap || childPlan.IsEmpty)
            return;

        Child?.Draw(childPlan.Size, context);
    }
}
