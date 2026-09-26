using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws a border on top of its child, inset within the child's bounds.
/// </summary>
/// <remarks>
/// The border deliberately consumes no layout space, matching how borders behave in CSS's <c>border-box</c>
/// model. Combine with padding when the content should be pushed away from the edge.
/// </remarks>
internal sealed class StrokeBlock : EnclosingBlock
{
    public Sides Weight { get; set; } = Sides.Zero;

    public Ink Ink { get; set; } = Ink.Black;

    /// <summary>
    /// Radius of the corner rounding. Only honoured when every side has the same width, since a rounded corner
    /// has no meaningful shape where two different thicknesses meet.
    /// </summary>
    public float CornerRadius { get; set; }

    /// <summary>True when all four sides share a width, which is what makes a corner radius meaningful.</summary>
    internal bool HasUniformWeight => Weight.Left > 0 && IsUniform;

    private bool IsUniform =>
        Math.Abs(Weight.Left - Weight.Top) < Extent.Epsilon
        && Math.Abs(Weight.Top - Weight.Right) < Extent.Epsilon
        && Math.Abs(Weight.Right - Weight.Bottom) < Extent.Epsilon;

    public override void Render(Extent availableSpace, RenderContext context)
    {
        Fit plan = Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        Child?.Render(availableSpace, context);

        if (Ink.IsTransparent)
            return;

        // Drawn around the whole box this element occupies (ADR 0012), not around its content's natural extent.
        Extent size = availableSpace;
        ISurface canvas = context.Surface;

        if (CornerRadius > 0 && HasUniformWeight)
        {
            // A stroke straddles the path, so the outline is drawn on the centreline: inset by half the width,
            // and reduce the radius to match, so that the stroke's *outer* arc lands on the requested radius and
            // coincides with a rounded background of the same value.
            float inset = Weight.Left / 2;
            Extent outline = new Extent(size.Width - Weight.Left, size.Height - Weight.Left);

            // Degenerate once the border is thicker than the box it surrounds; nothing sensible to draw.
            if (outline.Width <= 0 || outline.Height <= 0)
                return;

            float radius = Math.Clamp(
                CornerRadius - inset,
                0,
                Math.Min(outline.Width, outline.Height) / 2);

            canvas.DrawRoundedRectangle(new Offset(inset, inset), outline, radius, Ink, Weight.Left);

            return;
        }

        if (Weight.Left > 0)
            canvas.DrawRectangle(Offset.Zero, new Extent(Weight.Left, size.Height), Ink);

        if (Weight.Top > 0)
            canvas.DrawRectangle(Offset.Zero, new Extent(size.Width, Weight.Top), Ink);

        if (Weight.Right > 0)
            canvas.DrawRectangle(new Offset(size.Width - Weight.Right, 0), new Extent(Weight.Right, size.Height), Ink);

        if (Weight.Bottom > 0)
            canvas.DrawRectangle(new Offset(0, size.Height - Weight.Bottom), new Extent(size.Width, Weight.Bottom), Ink);
    }
}
