using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Rotates its child by whole quarter turns, swapping the measurement axes for odd turns.
/// </summary>
/// <remarks>
/// Unlike free rotation this participates in layout, which is what makes vertical table headers and side
/// captions possible.
/// </remarks>
public sealed class RotateElement : ContainerElement
{
    private int _quarterTurns;

    /// <summary>Number of clockwise quarter turns. Normalised into the range 0-3.</summary>
    public int QuarterTurns
    {
        get => _quarterTurns;
        set => _quarterTurns = ((value % 4) + 4) % 4;
    }

    private bool SwapsAxes => QuarterTurns is 1 or 3;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        Size innerSpace = SwapsAxes
            ? new Size(availableSpace.Height, availableSpace.Width)
            : availableSpace;

        SpacePlan childPlan = Child?.Measure(innerSpace, context) ?? SpacePlan.FullRender(Size.Zero);

        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsEmpty)
            return SpacePlan.Empty();

        Size size = SwapsAxes
            ? new Size(childPlan.Size.Height, childPlan.Size.Width)
            : childPlan.Size;

        return childPlan.IsFullRender ? SpacePlan.FullRender(size) : SpacePlan.PartialRender(size);
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        if (Child is null)
            return;

        Size innerSpace = SwapsAxes
            ? new Size(availableSpace.Height, availableSpace.Width)
            : availableSpace;

        SpacePlan childPlan = Child.Measure(innerSpace, context.Layout);

        if (childPlan.IsWrap || childPlan.IsEmpty)
            return;

        // Rotation happens about the origin, so translate the rotated content back into the positive quadrant.
        Position recentre = QuarterTurns switch
        {
            1 => new Position(childPlan.Size.Height, 0),
            2 => new Position(childPlan.Size.Width, childPlan.Size.Height),
            3 => new Position(0, childPlan.Size.Width),
            _ => Position.Zero
        };

        context.Canvas.Save();
        context.Canvas.Translate(recentre);
        context.Canvas.Rotate(QuarterTurns * 90f);
        Child.Draw(innerSpace, context);
        context.Canvas.Restore();
    }
}
