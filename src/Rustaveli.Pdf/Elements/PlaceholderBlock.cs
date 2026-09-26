using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A filled block standing in for content that does not exist yet.
/// </summary>
public sealed class PlaceholderBlock : Block
{
    public Ink Color { get; set; } = Ink.Rgb(0xEE, 0xEE, 0xEE);

    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        Fit.FullRender(availableSpace);

    public override void Draw(Extent availableSpace, RenderContext context) =>
        context.Canvas.DrawRectangle(Offset.Zero, availableSpace, Color);
}
