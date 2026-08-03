using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Mirrors its child about the centre of the space it occupies.
/// </summary>
/// <remarks>
/// Layout is unaffected — the mirrored content occupies exactly the same box it would have unmirrored.
/// </remarks>
public sealed class FlipElement : ContainerElement
{
    public bool FlipHorizontal { get; set; }

    public bool FlipVertical { get; set; }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        if (Child is null)
            return;

        SpacePlan plan = Child.Measure(availableSpace, context.Layout);

        if (plan.IsWrap || plan.IsEmpty)
            return;

        float scaleX = FlipHorizontal ? -1f : 1f;
        float scaleY = FlipVertical ? -1f : 1f;

        // Scaling by -1 reflects through the origin, which would put the content off the far side of it, so
        // translate by the full extent first to bring it back over its own box.
        Position offset = new Position(
            FlipHorizontal ? plan.Size.Width : 0,
            FlipVertical ? plan.Size.Height : 0);

        context.Canvas.Save();
        context.Canvas.Translate(offset);
        context.Canvas.Scale(scaleX, scaleY);

        // The child is drawn into exactly the box it was measured for, not the larger one on offer. Mirroring
        // about the measured size while letting the child position itself inside a wider box would throw
        // self-aligning content — right-aligned or right-to-left text — clean off the page.
        Child.Draw(plan.Size, context);

        context.Canvas.Restore();
    }
}
