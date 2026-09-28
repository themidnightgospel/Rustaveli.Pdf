using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Casts a shadow from the box its child occupies, drawn beneath the child and taking no room of its own.
/// </summary>
internal sealed class ShadowBlock : EnclosingBlock
{
    public Shadow Shadow { get; set; }

    /// <summary>The rounding of the box casting the shadow.</summary>
    public Corners Corners { get; set; }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        Fit plan = Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        // Cast from the whole box this element occupies (ADR 0012), as a fill behind it would cover it.
        context.Surface.DrawShadow(Offset.Zero, availableSpace, Corners, Shadow);
        Child?.Render(availableSpace, context);
    }
}
