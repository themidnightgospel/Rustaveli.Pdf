using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Paints a solid colour behind its child, covering exactly the area the child occupies.
/// </summary>
public sealed class FillBlock : EnclosingBlock
{
    public Ink Ink { get; set; } = Ink.Transparent;

    /// <summary>Radius of the corner rounding. Zero draws square corners.</summary>
    public float CornerRadius { get; set; }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        // A parent only draws what it measured as drawable; this guards callers that draw without asking.
        Fit plan = Plan(availableSpace, context.Layout);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        // The size given is the size this box occupies (ADR 0012), so the background fills all of it — a table
        // cell's full width and row height, not merely the extent of the text inside.
        if (!Ink.IsTransparent)
        {
            if (CornerRadius > 0)
                context.Canvas.DrawRoundedRectangle(Offset.Zero, availableSpace, CornerRadius, Ink);
            else
                context.Canvas.DrawRectangle(Offset.Zero, availableSpace, Ink);
        }

        Child?.Render(availableSpace, context);
    }
}
