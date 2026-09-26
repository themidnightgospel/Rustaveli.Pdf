using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Shifts its child by a fixed offset without affecting layout, allowing content to overlap its neighbours.
/// </summary>
internal sealed class ShiftBlock : EnclosingBlock
{
    public Offset Offset { get; set; } = Offset.Zero;

    public override void Render(Extent availableSpace, RenderContext context)
    {
        context.Surface.Translate(Offset);
        Child?.Render(availableSpace, context);
        context.Surface.Translate(Offset.Reverse());
    }
}
