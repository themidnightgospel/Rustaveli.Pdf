namespace Rustaveli.Pdf.Output;

/// <summary>
/// An affine transform as PDF writes one, <c>[a b c d e f]</c>, mapping (x, y) to (a·x + c·y + e, b·x + d·y + f).
/// </summary>
internal readonly record struct Transform(double A, double B, double C, double D, double E, double F)
{
    public static Transform Identity { get; } = new Transform(1, 0, 0, 1, 0, 0);

    /// <summary>
    /// Whether every entry can be written to a PDF. Transforms that each can may multiply into one that cannot, and even
    /// overflow a double.
    /// </summary>
    public bool IsWritable => Writable.Is(A) && Writable.Is(B) && Writable.Is(C) && Writable.Is(D) && Writable.Is(E) && Writable.Is(F);

    public static Transform Translation(double x, double y) => new Transform(1, 0, 0, 1, x, y);

    public static Transform Scaling(double x, double y) => new Transform(x, 0, 0, y, 0, 0);

    /// <summary>A turn by <paramref name="degrees"/>, clockwise in the engine's Y-down space.</summary>
    public static Transform Rotation(double degrees)
    {
        double radians = degrees * Math.PI / 180;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        return new Transform(cos, sin, -sin, cos, 0, 0);
    }

    /// <summary>
    /// The transform that applies <paramref name="first"/> and then this one: what the current transformation
    /// matrix becomes when a content stream concatenates <paramref name="first"/> with <c>cm</c>.
    /// </summary>
    public Transform After(Transform first) => new Transform(
        (first.A * A) + (first.B * C),
        (first.A * B) + (first.B * D),
        (first.C * A) + (first.D * C),
        (first.C * B) + (first.D * D),
        (first.E * A) + (first.F * C) + E,
        (first.E * B) + (first.F * D) + F);

    public (double X, double Y) Apply(double x, double y) => ((A * x) + (C * y) + E, (B * x) + (D * y) + F);
}
