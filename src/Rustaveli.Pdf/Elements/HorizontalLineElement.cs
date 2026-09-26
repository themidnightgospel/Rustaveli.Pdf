using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A solid rule spanning the available width.
/// </summary>
public sealed class HorizontalLineElement : Element
{
    public float Thickness { get; set; } = 1f;

    public Color Color { get; set; } = Colors.Black;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        Thickness > availableSpace.Height + Size.Epsilon
            ? SpacePlan.Wrap("The available height is smaller than the line thickness.")
            : SpacePlan.FullRender(new Size(availableSpace.Width, Thickness));

    public override void Draw(Size availableSpace, DrawContext context) =>
        context.Canvas.DrawRectangle(Position.Zero, new Size(availableSpace.Width, Thickness), Color);
}
