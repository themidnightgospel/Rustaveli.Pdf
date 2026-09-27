using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Mirrors its child about the centre of the space it occupies.
/// </summary>
/// <remarks>
/// Layout is unaffected — the mirrored content occupies exactly the same box it would have unmirrored.
/// </remarks>
internal sealed class MirrorBlock : EnclosingBlock
{
    public bool Horizontally { get; set; }

    public bool Vertically { get; set; }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Fit plan = Child.Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        float scaleX = Horizontally ? -1f : 1f;
        float scaleY = Vertically ? -1f : 1f;

        // Scaling by -1 reflects through the origin, which would put the content off the far side of it, so
        // translate by the full extent first to bring it back over its own box. The box is the one this element
        // was given (ADR 0012): the child is drawn into it, so it is also the extent to mirror across.
        Offset offset = new Offset(
            Horizontally ? availableSpace.Width : 0,
            Vertically ? availableSpace.Height : 0);

        context.Surface.Save();
        context.Surface.Translate(offset);
        context.Surface.Scale(scaleX, scaleY);
        Child.Render(availableSpace, context);
        context.Surface.Restore();
    }
}
