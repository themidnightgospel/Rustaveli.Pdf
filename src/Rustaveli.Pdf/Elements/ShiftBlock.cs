using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Shifts its child by a fixed offset without affecting layout, allowing content to overlap its neighbours.
/// </summary>
public sealed class ShiftBlock : EnclosingBlock
{
    public Offset Offset { get; set; } = Offset.Zero;

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        context.Canvas.Translate(Offset);
        Child?.Draw(availableSpace, context);
        context.Canvas.Translate(Offset.Reverse());
    }
}
