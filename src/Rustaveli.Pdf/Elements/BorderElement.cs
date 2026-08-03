using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Draws a border on top of its child, inset within the child's bounds.
/// </summary>
/// <remarks>
/// The border deliberately consumes no layout space, matching how borders behave in CSS's <c>border-box</c>
/// model. Combine with padding when the content should be pushed away from the edge.
/// </remarks>
public sealed class BorderElement : ContainerElement
{
    public Edges Width { get; set; } = Edges.Zero;

    public Color Color { get; set; } = Colors.Black;

    /// <summary>
    /// Radius of the corner rounding. Only honoured when every side has the same width, since a rounded corner
    /// has no meaningful shape where two different thicknesses meet.
    /// </summary>
    public float CornerRadius { get; set; }

    /// <summary>True when all four sides share a width, which is what makes a corner radius meaningful.</summary>
    internal bool HasUniformWidth => Width.Left > 0 && IsUniform;

    private bool IsUniform =>
        Math.Abs(Width.Left - Width.Top) < Size.Epsilon
        && Math.Abs(Width.Top - Width.Right) < Size.Epsilon
        && Math.Abs(Width.Right - Width.Bottom) < Size.Epsilon;

    public override void Draw(Size availableSpace, DrawContext context)
    {
        SpacePlan plan = Measure(availableSpace, context.Layout);

        if (plan.IsWrap || plan.IsEmpty)
            return;

        Child?.Draw(availableSpace, context);

        if (Color.IsTransparent)
            return;

        Size size = plan.Size;
        ICanvas canvas = context.Canvas;

        if (CornerRadius > 0 && HasUniformWidth)
        {
            // A stroke straddles the path, so the outline is drawn on the centreline: inset by half the width,
            // and reduce the radius to match, so that the stroke's *outer* arc lands on the requested radius and
            // coincides with a rounded background of the same value.
            float inset = Width.Left / 2;
            Size outline = new Size(size.Width - Width.Left, size.Height - Width.Left);

            // Degenerate once the border is thicker than the box it surrounds; nothing sensible to draw.
            if (outline.Width <= 0 || outline.Height <= 0)
                return;

            float radius = Math.Clamp(
                CornerRadius - inset,
                0,
                Math.Min(outline.Width, outline.Height) / 2);

            canvas.DrawRoundedRectangle(new Position(inset, inset), outline, radius, Color, Width.Left);

            return;
        }

        if (Width.Left > 0)
            canvas.DrawRectangle(Position.Zero, new Size(Width.Left, size.Height), Color);

        if (Width.Top > 0)
            canvas.DrawRectangle(Position.Zero, new Size(size.Width, Width.Top), Color);

        if (Width.Right > 0)
            canvas.DrawRectangle(new Position(size.Width - Width.Right, 0), new Size(Width.Right, size.Height), Color);

        if (Width.Bottom > 0)
            canvas.DrawRectangle(new Position(0, size.Height - Width.Bottom), new Size(size.Width, Width.Bottom), Color);
    }
}
