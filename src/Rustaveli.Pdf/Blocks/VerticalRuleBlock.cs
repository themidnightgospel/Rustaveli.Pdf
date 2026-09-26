using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A solid rule spanning the available height.
/// </summary>
internal sealed class VerticalRuleBlock : Block
{
    public float Weight { get; set; } = 1f;

    public Ink Ink { get; set; } = Ink.Black;

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        Weight > availableSpace.Width + Extent.Epsilon
            ? Fit.Defer("The width available is smaller than the rule's weight.")
            : Fit.Complete(new Extent(Weight, availableSpace.Height));

    public override void Render(Extent availableSpace, RenderContext context) =>
        context.Surface.DrawRectangle(Offset.Zero, new Extent(Weight, availableSpace.Height), Ink);
}
