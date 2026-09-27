using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Scales its child about the top-left corner. The reported size is scaled to match, so surrounding content
/// reflows around the visual result.
/// </summary>
internal sealed class ScaleBlock : EnclosingBlock
{
    public float ScaleX { get; set; } = 1f;

    public float ScaleY { get; set; } = 1f;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (ScaleX == 0 || ScaleY == 0)
            return Fit.Defer("A scale factor of zero collapses the content entirely.");

        // The child is measured in its own unscaled coordinate space, so expand the offered space by the
        // inverse of the scale before handing it over.
        Extent innerSpace = new Extent(
            availableSpace.Width / Math.Abs(ScaleX),
            availableSpace.Height / Math.Abs(ScaleY));

        Fit childPlan = Child?.Plan(innerSpace, context) ?? Fit.Complete(Extent.Zero);

        if (childPlan.IsDeferred)
            return childPlan;

        if (childPlan.IsNothing)
            return Fit.Nothing();

        Extent size = new Extent(
            childPlan.Size.Width * Math.Abs(ScaleX),
            childPlan.Size.Height * Math.Abs(ScaleY));

        return childPlan.IsComplete ? Fit.Complete(size) : Fit.Partial(size);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null || ScaleX == 0 || ScaleY == 0)
            return;

        Extent innerSpace = new Extent(
            availableSpace.Width / Math.Abs(ScaleX),
            availableSpace.Height / Math.Abs(ScaleY));

        // Undoing a scale by multiplying by its reciprocal loses precision, and the error compounds through
        // nested scales. Save and restore the transform instead, which is exact.
        context.Surface.Save();
        context.Surface.Translate(MirrorOffset(innerSpace, context.Planning));
        context.Surface.Scale(ScaleX, ScaleY);
        Child.Render(innerSpace, context);
        context.Surface.Restore();
    }

    /// <summary>
    /// A negative factor reflects the content through the origin, onto the far side of the box Measure reported.
    /// Shifting by the scaled extent on each mirrored axis brings it back over that box, as a flip does.
    /// </summary>
    private Offset MirrorOffset(Extent innerSpace, PlanContext context)
    {
        if (ScaleX > 0 && ScaleY > 0)
            return Offset.Zero;

        Extent childSize = Child!.Plan(innerSpace, context).Size;

        return new Offset(
            ScaleX < 0 ? childSize.Width * -ScaleX : 0f,
            ScaleY < 0 ? childSize.Height * -ScaleY : 0f);
    }
}
