using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A solid rule spanning the available width.
/// </summary>
internal sealed class RuleBlock : Block
{
    public float Thickness { get; set; } = 1f;

    public Ink Ink { get; set; } = Ink.Black;

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        Thickness > availableSpace.Height + Extent.Epsilon
            ? Fit.Defer("The available height is smaller than the line thickness.")
            : Fit.Complete(new Extent(availableSpace.Width, Thickness));

    public override void Render(Extent availableSpace, RenderContext context) =>
        context.Canvas.DrawRectangle(Offset.Zero, new Extent(availableSpace.Width, Thickness), Ink);
}
