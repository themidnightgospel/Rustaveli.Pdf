using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A filled block standing in for content that does not exist yet.
/// </summary>
internal sealed class PlaceholderBlock : Block
{
    public Ink Ink { get; set; } = Ink.Rgb(0xEE, 0xEE, 0xEE);

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        Fit.Complete(availableSpace);

    public override void Render(Extent availableSpace, RenderContext context) =>
        context.Canvas.DrawRectangle(Offset.Zero, availableSpace, Ink);
}
