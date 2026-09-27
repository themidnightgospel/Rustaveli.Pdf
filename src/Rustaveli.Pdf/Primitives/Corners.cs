namespace Rustaveli.Pdf;

/// <summary>
/// Per-corner radii in PDF points, for rounded fills and strokes: clockwise from the top left.
/// </summary>
public readonly record struct Corners(float TopLeft, float TopRight, float BottomRight, float BottomLeft)
{
    public static Corners Zero { get; } = new(0, 0, 0, 0);

    public static Corners All(float radius) => new(radius, radius, radius, radius);

    /// <summary>Whether any corner is rounded.</summary>
    public bool IsRounded => TopLeft > 0 || TopRight > 0 || BottomRight > 0 || BottomLeft > 0;

    public Corners WithTopLeft(float radius) => this with { TopLeft = radius };

    public Corners WithTopRight(float radius) => this with { TopRight = radius };

    public Corners WithBottomRight(float radius) => this with { BottomRight = radius };

    public Corners WithBottomLeft(float radius) => this with { BottomLeft = radius };

    /// <summary>
    /// The radii a box of <paramref name="size"/> can take: none negative, and scaled down together, as CSS scales
    /// them, wherever two corners on one side would otherwise overlap. Equal radii end at half the shorter side.
    /// </summary>
    internal Corners FittedTo(Extent size)
    {
        Corners corners = new Corners(Math.Max(0, TopLeft), Math.Max(0, TopRight), Math.Max(0, BottomRight), Math.Max(0, BottomLeft));
        float scale = 1f;

        scale = Math.Min(scale, Room(size.Width, corners.TopLeft + corners.TopRight));
        scale = Math.Min(scale, Room(size.Width, corners.BottomLeft + corners.BottomRight));
        scale = Math.Min(scale, Room(size.Height, corners.TopLeft + corners.BottomLeft));
        scale = Math.Min(scale, Room(size.Height, corners.TopRight + corners.BottomRight));

        return scale >= 1f
            ? corners
            : new Corners(corners.TopLeft * scale, corners.TopRight * scale, corners.BottomRight * scale, corners.BottomLeft * scale);
    }

    private static float Room(float side, float radii) => radii > side ? Math.Max(0, side) / radii : 1f;
}
