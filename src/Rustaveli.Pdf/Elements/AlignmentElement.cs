using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Claims the full space on any axis it aligns, then positions its child within that space.
/// </summary>
/// <remarks>
/// An axis with no alignment set is left untouched and collapses to the child's own size, so
/// <c>AlignRight</c> alone stretches horizontally while remaining vertically snug.
/// </remarks>
public sealed class AlignmentElement : EnclosingBlock
{
    public HorizontalPlacement? Horizontal { get; set; }

    public VerticalPlacement? Vertical { get; set; }

    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        Fit childPlan = Child?.Measure(availableSpace, context) ?? Fit.FullRender(Extent.Zero);

        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsEmpty)
            return Fit.Empty();

        Extent size = new Extent(
            Horizontal.HasValue ? availableSpace.Width : childPlan.Size.Width,
            Vertical.HasValue ? availableSpace.Height : childPlan.Size.Height);

        return childPlan.IsFullRender ? Fit.FullRender(size) : Fit.PartialRender(size);
    }

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Fit childPlan = Child.Measure(availableSpace, context.Layout);

        if (childPlan.IsWrap || childPlan.IsEmpty)
            return;

        Offset offset = new Offset(
            HorizontalOffset(availableSpace.Width, childPlan.Size.Width),
            VerticalOffset(availableSpace.Height, childPlan.Size.Height));

        context.Canvas.Translate(offset);

        // The child occupies the box it measured, placed by the offset above (ADR 0012). Given the whole space
        // instead, content that positions itself — right-aligned or right-to-left text — would be offset a second
        // time, off the far edge. Text re-wrapped at its own measured width reproduces the same lines: every line
        // already fits, and none can take a word more than it did in the wider box.
        Child.Draw(childPlan.Size, context);

        context.Canvas.Translate(offset.Reverse());
    }

    private float HorizontalOffset(float available, float child) => Horizontal switch
    {
        HorizontalPlacement.Center => (available - child) / 2,
        HorizontalPlacement.Right => available - child,
        _ => 0f
    };

    private float VerticalOffset(float available, float child) => Vertical switch
    {
        VerticalPlacement.Middle => (available - child) / 2,
        VerticalPlacement.Bottom => available - child,
        _ => 0f
    };
}
