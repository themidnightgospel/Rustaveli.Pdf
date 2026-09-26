using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Mirrors its child about the centre of the space it occupies.
/// </summary>
/// <remarks>
/// Layout is unaffected — the mirrored content occupies exactly the same box it would have unmirrored.
/// </remarks>
public sealed class FlipElement : EnclosingBlock
{
    public bool FlipHorizontal { get; set; }

    public bool FlipVertical { get; set; }

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Fit plan = Child.Measure(availableSpace, context.Layout);

        if (plan.IsWrap || plan.IsEmpty)
            return;

        float scaleX = FlipHorizontal ? -1f : 1f;
        float scaleY = FlipVertical ? -1f : 1f;

        // Scaling by -1 reflects through the origin, which would put the content off the far side of it, so
        // translate by the full extent first to bring it back over its own box. The box is the one this element
        // was given (ADR 0012): the child is drawn into it, so it is also the extent to mirror across.
        Offset offset = new Offset(
            FlipHorizontal ? availableSpace.Width : 0,
            FlipVertical ? availableSpace.Height : 0);

        context.Canvas.Save();
        context.Canvas.Translate(offset);
        context.Canvas.Scale(scaleX, scaleY);
        Child.Draw(availableSpace, context);
        context.Canvas.Restore();
    }
}
