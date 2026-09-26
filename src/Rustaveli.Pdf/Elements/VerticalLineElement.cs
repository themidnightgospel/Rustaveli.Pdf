using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A solid rule spanning the available height.
/// </summary>
public sealed class VerticalLineElement : Element
{
    public float Thickness { get; set; } = 1f;

    public Ink Color { get; set; } = Ink.Black;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        Thickness > availableSpace.Width + Size.Epsilon
            ? SpacePlan.Wrap("The available width is smaller than the line thickness.")
            : SpacePlan.FullRender(new Size(Thickness, availableSpace.Height));

    public override void Draw(Size availableSpace, DrawContext context) =>
        context.Canvas.DrawRectangle(Position.Zero, new Size(Thickness, availableSpace.Height), Color);
}
