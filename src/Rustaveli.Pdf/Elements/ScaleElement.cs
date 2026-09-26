using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Scales its child about the top-left corner. The reported size is scaled to match, so surrounding content
/// reflows around the visual result.
/// </summary>
public sealed class ScaleElement : ContainerElement
{
    public float ScaleX { get; set; } = 1f;

    public float ScaleY { get; set; } = 1f;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        if (ScaleX == 0 || ScaleY == 0)
            return SpacePlan.Wrap("A scale factor of zero collapses the content entirely.");

        // The child is measured in its own unscaled coordinate space, so expand the offered space by the
        // inverse of the scale before handing it over.
        Size innerSpace = new Size(
            availableSpace.Width / Math.Abs(ScaleX),
            availableSpace.Height / Math.Abs(ScaleY));

        SpacePlan childPlan = Child?.Measure(innerSpace, context) ?? SpacePlan.FullRender(Size.Zero);

        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsEmpty)
            return SpacePlan.Empty();

        Size size = new Size(
            childPlan.Size.Width * Math.Abs(ScaleX),
            childPlan.Size.Height * Math.Abs(ScaleY));

        return childPlan.IsFullRender ? SpacePlan.FullRender(size) : SpacePlan.PartialRender(size);
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        if (Child is null || ScaleX == 0 || ScaleY == 0)
            return;

        Size innerSpace = new Size(
            availableSpace.Width / Math.Abs(ScaleX),
            availableSpace.Height / Math.Abs(ScaleY));

        // Undoing a scale by multiplying by its reciprocal loses precision, and the error compounds through
        // nested scales. Save and restore the transform instead, which is exact.
        context.Canvas.Save();
        context.Canvas.Translate(MirrorOffset(innerSpace, context.Layout));
        context.Canvas.Scale(ScaleX, ScaleY);
        Child.Draw(innerSpace, context);
        context.Canvas.Restore();
    }

    /// <summary>
    /// A negative factor reflects the content through the origin, onto the far side of the box Measure reported.
    /// Shifting by the scaled extent on each mirrored axis brings it back over that box, as a flip does.
    /// </summary>
    private Position MirrorOffset(Size innerSpace, LayoutContext context)
    {
        if (ScaleX > 0 && ScaleY > 0)
            return Position.Zero;

        Size childSize = Child!.Measure(innerSpace, context).Size;

        return new Position(
            ScaleX < 0 ? childSize.Width * -ScaleX : 0f,
            ScaleY < 0 ? childSize.Height * -ScaleY : 0f);
    }
}
