using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A rule spanning the available width: solid, or in any <see cref="StrokeStyle"/> or pattern of dashes.
/// </summary>
internal sealed class RuleBlock : Block
{
    public float Weight { get; set; } = 1f;

    public Ink Ink { get; set; } = Ink.Black;

    public StrokeStyle Style { get; set; }

    /// <summary>Lengths of dash and gap, alternating, in place of <see cref="Style"/> when set.</summary>
    public IReadOnlyList<float>? Dashes { get; set; }

    /// <summary>Painted in place of <see cref="Ink"/> when set, along the rule's length.</summary>
    public Gradient? Gradient { get; set; }

    private RuleStroke Stroke => new RuleStroke(Weight, Ink, Style, Dashes, Gradient);

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        float breadth = Stroke.Breadth;

        return breadth > availableSpace.Height + Extent.Epsilon
            ? Fit.Defer("The height available is smaller than the rule's weight.")
            : Fit.Complete(new Extent(availableSpace.Width, breadth));
    }

    public override void Render(Extent availableSpace, RenderContext context) =>
        Stroke.Draw(context.Surface, new Extent(availableSpace.Width, Stroke.Breadth), across: true);
}
