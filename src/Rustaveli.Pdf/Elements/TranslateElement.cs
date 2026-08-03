using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Shifts its child by a fixed offset without affecting layout, allowing content to overlap its neighbours.
/// </summary>
public sealed class TranslateElement : ContainerElement
{
    public Position Offset { get; set; } = Position.Zero;

    public override void Draw(Size availableSpace, DrawContext context)
    {
        context.Canvas.Translate(Offset);
        Child?.Draw(availableSpace, context);
        context.Canvas.Translate(Offset.Reverse());
    }
}
