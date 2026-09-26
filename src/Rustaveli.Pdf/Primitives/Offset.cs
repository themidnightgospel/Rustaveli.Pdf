namespace Rustaveli.Pdf.Primitives;

/// <summary>
/// An offset in PDF points from the top-left of the current coordinate space.
/// </summary>
public readonly record struct Offset(float X, float Y)
{
    public static Offset Zero { get; } = new(0, 0);

    public Offset Reverse() => new(-X, -Y);

    public static Offset operator +(Offset a, Offset b) => new(a.X + b.X, a.Y + b.Y);

    public override string ToString() => $"(X: {X:F3}, Y: {Y:F3})";
}
