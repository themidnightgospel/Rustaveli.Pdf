namespace Rustaveli.Pdf.Primitives;

/// <summary>
/// An offset in PDF points from the top-left of the current coordinate space.
/// </summary>
public readonly record struct Position(float X, float Y)
{
    public static Position Zero { get; } = new(0, 0);

    public Position Reverse() => new(-X, -Y);

    public static Position operator +(Position a, Position b) => new(a.X + b.X, a.Y + b.Y);

    public override string ToString() => $"(X: {X:F3}, Y: {Y:F3})";
}
