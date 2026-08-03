using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Insets its child by a fixed amount on each side.
/// </summary>
public sealed class PaddingElement : ContainerElement
{
    public Edges Padding { get; set; } = Edges.Zero;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        Size innerSpace = new Size(
            availableSpace.Width - Padding.Horizontal,
            availableSpace.Height - Padding.Vertical);

        if (innerSpace.IsNegative)
            return SpacePlan.Wrap("The available space is smaller than the requested padding.");

        SpacePlan childPlan = Child?.Measure(innerSpace, context) ?? SpacePlan.FullRender(Size.Zero);

        if (childPlan.IsWrap)
            return childPlan;

        // A child with nothing left to draw must not resurrect the padding on the next page.
        if (childPlan.IsEmpty)
            return SpacePlan.Empty();

        Size size = new Size(
            childPlan.Size.Width + Padding.Horizontal,
            childPlan.Size.Height + Padding.Vertical);

        return childPlan.IsFullRender ? SpacePlan.FullRender(size) : SpacePlan.PartialRender(size);
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        if (Child is null)
            return;

        Size innerSpace = new Size(
            availableSpace.Width - Padding.Horizontal,
            availableSpace.Height - Padding.Vertical);

        if (innerSpace.IsNegative)
            return;

        context.Canvas.Translate(new Position(Padding.Left, Padding.Top));
        Child.Draw(innerSpace, context);
        context.Canvas.Translate(new Position(Padding.Left, Padding.Top).Reverse());
    }
}
