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
public sealed class TurnBlock : EnclosingBlock
{
    private int _quarterTurns;

    /// <summary>Number of clockwise quarter turns. Normalised into the range 0-3.</summary>
    public int QuarterTurns
    {
        get => _quarterTurns;
        set => _quarterTurns = ((value % 4) + 4) % 4;
    }

    private bool SwapsAxes => QuarterTurns is 1 or 3;

    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        Extent innerSpace = SwapsAxes
            ? new Extent(availableSpace.Height, availableSpace.Width)
            : availableSpace;

        Fit childPlan = Child?.Measure(innerSpace, context) ?? Fit.FullRender(Extent.Zero);

        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsEmpty)
            return Fit.Empty();

        Extent size = SwapsAxes
            ? new Extent(childPlan.Size.Height, childPlan.Size.Width)
            : childPlan.Size;

        return childPlan.IsFullRender ? Fit.FullRender(size) : Fit.PartialRender(size);
    }

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Extent innerSpace = SwapsAxes
            ? new Extent(availableSpace.Height, availableSpace.Width)
            : availableSpace;

        Fit childPlan = Child.Measure(innerSpace, context.Layout);

        if (childPlan.IsWrap || childPlan.IsEmpty)
            return;

        // Rotation happens about the origin, so translate the rotated content back into the positive quadrant.
        // The pivot is the box this element was given (ADR 0012), which is what the child is drawn into; pivoting
        // about the child's natural size instead would misplace content that fills or aligns within its box.
        Offset recentre = QuarterTurns switch
        {
            1 => new Offset(innerSpace.Height, 0),
            2 => new Offset(innerSpace.Width, innerSpace.Height),
            3 => new Offset(0, innerSpace.Width),
            _ => Offset.Zero
        };

        context.Canvas.Save();
        context.Canvas.Translate(recentre);
        context.Canvas.Rotate(QuarterTurns * 90f);
        Child.Draw(innerSpace, context);
        context.Canvas.Restore();
    }
}
