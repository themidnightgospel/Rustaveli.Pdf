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
        // Re-measuring is safe because Measure is required to be side-effect free, and it is the only way to
        // learn how much of the offered space the child will actually claim.
        SpacePlan plan = Measure(availableSpace, context.Layout);

        if (plan.IsWrap || plan.IsEmpty)
            return;

        if (!Color.IsTransparent)
        {
            if (CornerRadius > 0)
                context.Canvas.DrawRoundedRectangle(Position.Zero, plan.Size, CornerRadius, Color);
            else
                context.Canvas.DrawRectangle(Position.Zero, plan.Size, Color);
        }

        Child?.Draw(availableSpace, context);
    }
}
