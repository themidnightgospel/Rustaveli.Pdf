using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A filled block standing in for content that does not exist yet.
/// </summary>
public sealed class PlaceholderElement : Element
{
    public Ink Color { get; set; } = Ink.Rgb(0xEE, 0xEE, 0xEE);

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        SpacePlan.FullRender(availableSpace);

    public override void Draw(Size availableSpace, DrawContext context) =>
        context.Canvas.DrawRectangle(Position.Zero, availableSpace, Color);
}
