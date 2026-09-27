namespace Rustaveli.Pdf;

/// <summary>
/// A soft shadow cast by a frame onto what lies beneath it, as a drop shadow in a layout application or a box
/// shadow in CSS.
/// </summary>
/// <param name="Ink">The shadow's colour and opacity.</param>
/// <param name="Blur">
/// How far the shadow's edge fades, in points: its blur radius, twice the deviation of the Gaussian that softens it.
/// Zero leaves a sharp edge.
/// </param>
/// <param name="Offset">How far the shadow falls from the frame: right and down for positive values.</param>
/// <param name="Spread">How much larger than the frame the shadow is on every side; negative makes it smaller.</param>
public readonly record struct Shadow(Ink Ink, float Blur, Offset Offset = default, float Spread = 0)
{
    /// <summary>The shape the shadow takes before it is blurred: the frame moved by the offset and grown by the spread.</summary>
    internal (Offset Position, Extent Size, Corners Corners) Shape(Offset position, Extent size, Corners corners)
    {
        float spread = Spread;
        Extent grown = new Extent(size.Width + (2 * spread), size.Height + (2 * spread));

        // As in CSS, a rounded corner grows with the shadow and a square one stays square.
        Corners radii = new Corners(Grown(corners.TopLeft), Grown(corners.TopRight), Grown(corners.BottomRight), Grown(corners.BottomLeft));

        return (position + Offset + new Offset(-spread, -spread), grown, radii.FittedTo(grown));

        float Grown(float radius) => radius > 0 ? Math.Max(0, radius + spread) : 0f;
    }

    /// <summary>The deviation of the Gaussian that blurs the shadow, in points.</summary>
    internal float Deviation => Math.Max(0, Blur) / 2;
}
