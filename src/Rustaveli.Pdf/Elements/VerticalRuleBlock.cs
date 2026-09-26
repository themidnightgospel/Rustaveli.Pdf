using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A solid rule spanning the available height.
/// </summary>
public sealed class VerticalRuleBlock : Block
{
    public float Thickness { get; set; } = 1f;

    public Ink Ink { get; set; } = Ink.Black;

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        Thickness > availableSpace.Width + Extent.Epsilon
            ? Fit.Defer("The available width is smaller than the line thickness.")
            : Fit.Complete(new Extent(Thickness, availableSpace.Height));

    public override void Render(Extent availableSpace, RenderContext context) =>
        context.Canvas.DrawRectangle(Offset.Zero, new Extent(Thickness, availableSpace.Height), Ink);
}
