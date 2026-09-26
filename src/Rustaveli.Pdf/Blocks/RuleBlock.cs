using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A solid rule spanning the available width.
/// </summary>
internal sealed class RuleBlock : Block
{
    public float Weight { get; set; } = 1f;

    public Ink Ink { get; set; } = Ink.Black;

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        Weight > availableSpace.Height + Extent.Epsilon
            ? Fit.Defer("The height available is smaller than the rule's weight.")
            : Fit.Complete(new Extent(availableSpace.Width, Weight));

    public override void Render(Extent availableSpace, RenderContext context) =>
        context.Surface.DrawRectangle(Offset.Zero, new Extent(availableSpace.Width, Weight), Ink);
}
