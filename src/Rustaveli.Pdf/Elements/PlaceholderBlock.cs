using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// A filled block standing in for content that does not exist yet.
/// </summary>
public sealed class PlaceholderBlock : Block
{
    public Ink Ink { get; set; } = Ink.Rgb(0xEE, 0xEE, 0xEE);

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        Fit.Complete(availableSpace);

    public override void Render(Extent availableSpace, RenderContext context) =>
        context.Canvas.DrawRectangle(Offset.Zero, availableSpace, Ink);
}
