namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// The shapes behind the stroke styles, computed once so that every surface draws the same double lines and waves.
/// </summary>
internal static class StrokeGeometry
{
    /// <summary>A cubic reaches the height of a half sine wave when its control points sit this far above it.</summary>
    private const float ArchControlRatio = 4f / 3f;

    /// <summary>
    /// How far each line of a double stroke sits from the stroke's centre line: one weight, on either side, so
    /// two lines each the full weight have a weight's gap between them, as a word processor draws them. Lines a
    /// third as thick would fit the single stroke's weight, but merge into one at text sizes.
    /// </summary>
    public static Offset DoubleOffset(Offset from, Offset to, float thickness)
    {
        (float normalX, float normalY) = Normal(from, to);
        return new Offset(normalX * thickness, normalY * thickness);
    }

    /// <summary>
    /// A wave along the line, a thickness either side of it, in whole half-waves about twice the thickness long —
    /// stretched slightly so the last one ends exactly at <paramref name="to"/>. The first arch rises.
    /// </summary>
    public static IReadOnlyList<CubicSegment> Wave(Offset from, Offset to, float thickness)
    {
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float length = (float)Math.Sqrt((dx * dx) + (dy * dy));

        if (length <= 0)
            return [];

        float unitX = dx / length;
        float unitY = dy / length;
        float normalX = -unitY;
        float normalY = unitX;
        int arches = Math.Max(1, (int)Math.Round(length / (thickness * 2), MidpointRounding.AwayFromZero));
        float step = length / arches;
        float lift = thickness * ArchControlRatio;
        List<CubicSegment> segments = new List<CubicSegment>(arches);

        for (int index = 0; index < arches; index++)
        {
            // In the engine's space Y runs down, so rising is negative along the normal.
            float sign = index % 2 == 0 ? -1f : 1f;
            float start = step * index;

            segments.Add(new CubicSegment(
                Along(start + (step / 3), sign * lift),
                Along(start + (step * 2 / 3), sign * lift),
                Along(start + step, 0f)));
        }

        return segments;

        Offset Along(float distance, float offset) => new Offset(
            from.X + (unitX * distance) + (normalX * offset),
            from.Y + (unitY * distance) + (normalY * offset));
    }

    /// <summary>The unit normal to the line, turned a quarter clockwise; for a zero-length line, straight down.</summary>
    private static (float X, float Y) Normal(Offset from, Offset to)
    {
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float length = (float)Math.Sqrt((dx * dx) + (dy * dy));

        return length > 0 ? (-dy / length, dx / length) : (0f, 1f);
    }
}
