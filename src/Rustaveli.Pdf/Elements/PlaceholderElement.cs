using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A filled block standing in for content that does not exist yet.
/// </summary>
public sealed class PlaceholderElement : Element
{
    public Color Color { get; set; } = Colors.Grey.Lighten3;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        SpacePlan.FullRender(availableSpace);

    public override void Draw(Size availableSpace, DrawContext context) =>
        context.Canvas.DrawRectangle(Position.Zero, availableSpace, Color);
}
