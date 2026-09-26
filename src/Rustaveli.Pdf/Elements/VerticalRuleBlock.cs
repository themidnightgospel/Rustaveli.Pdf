using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A solid rule spanning the available height.
/// </summary>
public sealed class VerticalRuleBlock : Block
{
    public float Thickness { get; set; } = 1f;

    public Ink Color { get; set; } = Ink.Black;

    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        Thickness > availableSpace.Width + Extent.Epsilon
            ? Fit.Wrap("The available width is smaller than the line thickness.")
            : Fit.FullRender(new Extent(Thickness, availableSpace.Height));

    public override void Draw(Extent availableSpace, RenderContext context) =>
        context.Canvas.DrawRectangle(Offset.Zero, new Extent(Thickness, availableSpace.Height), Color);
}
