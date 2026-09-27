namespace Rustaveli.Pdf.Output;

/// <summary>
/// An affine transform as PDF writes one, <c>[a b c d e f]</c>, mapping (x, y) to (a·x + c·y + e, b·x + d·y + f).
/// </summary>
internal readonly record struct Transform(double A, double B, double C, double D, double E, double F)
{
    public static Transform Identity { get; } = new Transform(1, 0, 0, 1, 0, 0);

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
