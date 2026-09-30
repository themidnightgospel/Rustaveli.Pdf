using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Scales its child from its top left corner and takes the room the scaled child covers, so the content around it
/// makes way. A factor below zero mirrors the child along that axis.
/// </summary>
internal sealed class ScaleBlock : EnclosingBlock
{
    public float ScaleX { get; set; } = 1f;

    public float ScaleY { get; set; } = 1f;

    private bool Collapses => ScaleX == 0 || ScaleY == 0;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (Collapses)
            return Fit.Defer("A scale factor of zero collapses the content entirely.");

        Fit plan = base.PlanCore(Unscaled(availableSpace), context);
        return Resized(plan, new Extent(plan.Size.Width * Math.Abs(ScaleX), plan.Size.Height * Math.Abs(ScaleY)));
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null || Collapses)
            return;

        Extent room = Unscaled(availableSpace);
        Fit plan = Child.Plan(room, context.Planning);

        // Mirrored through the origin, the content would land on the far side of it, off the box this block reports;
        // moving the origin across that box first brings it back over it.
        Offset across = new Offset(
            ScaleX < 0 ? plan.Size.Width * -ScaleX : 0,
            ScaleY < 0 ? plan.Size.Height * -ScaleY : 0);

        context.Surface.Save();
        context.Surface.MoveOrigin(across);
        context.Surface.ScaleAxes(ScaleX, ScaleY);
        Child.Render(room, context);
        context.Surface.Restore();
    }

    /// <summary>The room the child has in its own units, which scaling makes into the room offered.</summary>
    private Extent Unscaled(Extent room) => new Extent(room.Width / Math.Abs(ScaleX), room.Height / Math.Abs(ScaleY));
}
