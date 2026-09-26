using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Paints a solid colour behind its child, covering exactly the area the child occupies.
/// </summary>
public sealed class BackgroundElement : ContainerElement
{
    public Color Color { get; set; } = Colors.Transparent;

    /// <summary>Radius of the corner rounding. Zero draws square corners.</summary>
    public float CornerRadius { get; set; }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        // A parent only draws what it measured as drawable; this guards callers that draw without asking.
        SpacePlan plan = Measure(availableSpace, context.Layout);

        if (plan.IsWrap || plan.IsEmpty)
            return;

        // The size given is the size this box occupies (ADR 0012), so the background fills all of it — a table
        // cell's full width and row height, not merely the extent of the text inside.
        if (!Color.IsTransparent)
        {
            if (CornerRadius > 0)
                context.Canvas.DrawRoundedRectangle(Position.Zero, availableSpace, CornerRadius, Color);
            else
                context.Canvas.DrawRectangle(Position.Zero, availableSpace, Color);
        }

        Child?.Draw(availableSpace, context);
    }
}
