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
public sealed class AlignmentElement : ContainerElement
{
    public HorizontalAlignment? Horizontal { get; set; }

    public VerticalAlignment? Vertical { get; set; }

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        SpacePlan childPlan = Child?.Measure(availableSpace, context) ?? SpacePlan.FullRender(Size.Zero);

        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsEmpty)
            return SpacePlan.Empty();

        Size size = new Size(
            Horizontal.HasValue ? availableSpace.Width : childPlan.Size.Width,
            Vertical.HasValue ? availableSpace.Height : childPlan.Size.Height);

        return childPlan.IsFullRender ? SpacePlan.FullRender(size) : SpacePlan.PartialRender(size);
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        if (Child is null)
            return;

        SpacePlan childPlan = Child.Measure(availableSpace, context.Layout);

        if (childPlan.IsWrap || childPlan.IsEmpty)
            return;

        Position offset = new Position(
            HorizontalOffset(availableSpace.Width, childPlan.Size.Width),
            VerticalOffset(availableSpace.Height, childPlan.Size.Height));

        context.Canvas.Translate(offset);

        // Drawing against the same space that was measured guarantees the child makes identical internal
        // decisions — text in particular must not be given a narrower box or it would re-wrap.
        Child.Draw(availableSpace, context);

        context.Canvas.Translate(offset.Reverse());
    }

    private float HorizontalOffset(float available, float child) => Horizontal switch
    {
        HorizontalAlignment.Center => (available - child) / 2,
        HorizontalAlignment.Right => available - child,
        _ => 0f
    };

    private float VerticalOffset(float available, float child) => Vertical switch
    {
        VerticalAlignment.Middle => (available - child) / 2,
        VerticalAlignment.Bottom => available - child,
        _ => 0f
    };
}
