using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A rule spanning the available height: solid, or in any <see cref="StrokeStyle"/> or pattern of dashes.
/// </summary>
internal sealed class VerticalRuleBlock : Block
{
    public float Weight { get; set; } = 1f;

    public Ink Ink { get; set; } = Ink.Black;

    public StrokeStyle Style { get; set; }

    /// <summary>Lengths of dash and gap, alternating, in place of <see cref="Style"/> when set.</summary>
    public IReadOnlyList<float>? Dashes { get; set; }

    private RuleStroke Stroke => new RuleStroke(Weight, Ink, Style, Dashes);

    public override Fit Plan(Extent availableSpace, PlanContext context)
    {
        float breadth = Stroke.Breadth;

        return breadth > availableSpace.Width + Extent.Epsilon
            ? Fit.Defer("The width available is smaller than the rule's weight.")
            : Fit.Complete(new Extent(breadth, availableSpace.Height));
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (Dashes is null && Style == StrokeStyle.Solid)
        {
            context.Surface.DrawRectangle(Offset.Zero, new Extent(Weight, availableSpace.Height), Ink);
            return;
        }

        RuleStroke stroke = Stroke;
        float middle = stroke.Breadth / 2;
        stroke.Draw(context.Surface, new Offset(middle, 0), new Offset(middle, availableSpace.Height));
    }
}
