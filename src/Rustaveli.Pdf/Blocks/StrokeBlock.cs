using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws a border on top of its child, along the edge of the child's bounds.
/// </summary>
/// <remarks>
/// The border deliberately consumes no layout space, matching how borders behave in CSS's <c>border-box</c>
/// model. Combine with padding when the content should be pushed away from the edge. Aligned inside, the default,
/// it lies within the bounds; centred or outside, it reaches past them, as a layout application's frame stroke does.
/// </remarks>
internal sealed class StrokeBlock : EnclosingBlock
{
    public Sides Weight { get; set; } = Sides.Zero;

    public Ink Ink { get; set; } = Ink.Black;

    /// <summary>Painted in place of <see cref="Ink"/> when set, across the whole outer box of the stroke.</summary>
    public Gradient? Gradient { get; set; }

    /// <summary>
    /// The rounding of each corner. Only honoured when every side has the same width, since a rounded corner has no
    /// meaningful shape where two different thicknesses meet.
    /// </summary>
    public Corners Corners { get; set; }

    /// <summary>Where the stroke lies against the edge.</summary>
    public StrokeAlignment Alignment { get; set; }

    /// <summary>True when all four sides share a width, which is what makes a corner radius meaningful.</summary>
    internal bool HasUniformWeight => Weight.Left > 0 && IsUniform;

    private bool IsUniform =>
        Math.Abs(Weight.Left - Weight.Top) < Extent.Epsilon
        && Math.Abs(Weight.Top - Weight.Right) < Extent.Epsilon
        && Math.Abs(Weight.Right - Weight.Bottom) < Extent.Epsilon;

    /// <summary>How much of each side's weight lies beyond the edge: none inside, half centred, all outside.</summary>
    private float Beyond => Alignment switch
    {
        StrokeAlignment.Center => 0.5f,
        StrokeAlignment.Outside => 1f,
        _ => 0f,
    };

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        Fit plan = Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        Child?.Render(availableSpace, context);

        if (Gradient is null && Ink.IsTransparent)
            return;

        // Drawn around the whole box this element occupies (ADR 0012), not around its content's natural extent.
        Extent size = availableSpace;
        ISurface surface = context.Surface;
        float beyond = Beyond;

        if (Gradient is null)
        {
            Draw(surface, size, Ink);
            return;
        }

        // One blend across everything the stroke covers, rather than one per side.
        Offset outer = new Offset(-Weight.Left * beyond, -Weight.Top * beyond);
        Extent outerSize = new Extent(
            size.Width + ((Weight.Left + Weight.Right) * beyond),
            size.Height + ((Weight.Top + Weight.Bottom) * beyond));

        surface.BeginGradient(Gradient, outer, outerSize);
        Draw(surface, size, Ink.Black);
        surface.EndGradient();
    }

    private void Draw(ISurface surface, Extent size, Ink ink)
    {
        if (Corners.IsRounded && HasUniformWeight)
        {
            DrawRounded(surface, size, ink);
            return;
        }

        float beyond = Beyond;
        float left = Weight.Left * beyond;
        float top = Weight.Top * beyond;
        float right = Weight.Right * beyond;
        float bottom = Weight.Bottom * beyond;

        // Each side runs the full length of the outer edge, so the corners are filled wherever the stroke lies.
        float height = size.Height + top + bottom;
        float width = size.Width + left + right;

        if (Weight.Left > 0)
            surface.DrawRectangle(new Offset(-left, -top), new Extent(Weight.Left, height), ink);

        if (Weight.Top > 0)
            surface.DrawRectangle(new Offset(-left, -top), new Extent(width, Weight.Top), ink);

        if (Weight.Right > 0)
            surface.DrawRectangle(new Offset(size.Width + right - Weight.Right, -top), new Extent(Weight.Right, height), ink);

        if (Weight.Bottom > 0)
            surface.DrawRectangle(new Offset(-left, size.Height + bottom - Weight.Bottom), new Extent(width, Weight.Bottom), ink);
    }

    /// <summary>
    /// A stroke straddles its path, so a rounded outline is drawn along the stroke's centre line, and each radius
    /// moved by as much as the line: aligned inside, the stroke's outer arc lands on the requested radius and
    /// coincides with a rounded background of the same value; centred, the centre line does; outside, the inner arc.
    /// </summary>
    private void DrawRounded(ISurface surface, Extent size, Ink ink)
    {
        float weight = Weight.Left;
        float inset = (weight / 2) - (weight * Beyond);
        Extent outline = new Extent(size.Width - (2 * inset), size.Height - (2 * inset));

        // Degenerate once the border is thicker than the box it surrounds; nothing sensible to draw.
        if (outline.Width <= 0 || outline.Height <= 0)
            return;

        Corners corners = Corners;
        Corners radii = new Corners(Moved(corners.TopLeft), Moved(corners.TopRight), Moved(corners.BottomRight), Moved(corners.BottomLeft))
            .FittedTo(outline);

        surface.DrawRoundedRectangle(new Offset(inset, inset), outline, radii, ink, weight);

        // A square corner stays square wherever the stroke lies.
        float Moved(float radius) => radius > 0 ? Math.Max(0, radius - inset) : 0f;
    }
}
