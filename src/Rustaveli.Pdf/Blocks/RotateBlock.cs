using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Rotates its child by any angle about the centre of its box, without affecting layout: the content keeps the
/// room it was given and may reach past it, as <see cref="ShiftBlock"/>'s content may.
/// </summary>
/// <remarks>
/// Quarter turns that should take part in layout, swapping width and height, are <see cref="TurnBlock"/>'s job.
/// </remarks>
internal sealed class RotateBlock : EnclosingBlock
{
    /// <summary>The angle in degrees, clockwise on the page.</summary>
    public float Degrees { get; set; }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        // The pivot is the box this element was given (ADR 0012), which is what the child is drawn into.
        Offset centre = new Offset(availableSpace.Width / 2, availableSpace.Height / 2);

        context.Surface.Save();
        context.Surface.Translate(centre);
        context.Surface.Rotate(Degrees);
        context.Surface.Translate(centre.Reverse());
        Child.Render(availableSpace, context);
        context.Surface.Restore();
    }
}
