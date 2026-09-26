using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Refuses to split its child across pages, deferring the whole thing rather than drawing part of it.
/// </summary>
/// <remarks>
/// Converts a partial render into a wrap, which sends the content to the next page intact. If it cannot fit on
/// an empty page either, the engine reports a layout failure rather than silently truncating.
/// </remarks>
public sealed class ShowEntireElement : ContainerElement
{
    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        SpacePlan childPlan = base.Measure(availableSpace, context);

        return childPlan.IsPartialRender
            ? SpacePlan.Wrap("The content is kept together and does not fit in the remaining space.")
            : childPlan;
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        // Measure guarantees the parent only draws this when the whole child fits.
        if (Measure(availableSpace, context.Layout).IsWrap)
            return;

        base.Draw(availableSpace, context);
    }
}
