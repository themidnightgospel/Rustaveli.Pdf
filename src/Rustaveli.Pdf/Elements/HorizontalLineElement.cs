using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A solid rule spanning the available width.
/// </summary>
public sealed class HorizontalLineElement : Block
{
    public float Thickness { get; set; } = 1f;

    public Ink Color { get; set; } = Ink.Black;

    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        Thickness > availableSpace.Height + Extent.Epsilon
            ? Fit.Wrap("The available height is smaller than the line thickness.")
            : Fit.FullRender(new Extent(availableSpace.Width, Thickness));

    public override void Draw(Extent availableSpace, RenderContext context) =>
        context.Canvas.DrawRectangle(Offset.Zero, new Extent(availableSpace.Width, Thickness), Color);
}
