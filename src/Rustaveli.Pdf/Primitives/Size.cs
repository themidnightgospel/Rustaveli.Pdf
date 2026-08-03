namespace Rustaveli.Pdf.Primitives;

/// <summary>
/// A width/height pair expressed in PDF points.
/// </summary>
public readonly record struct Size(float Width, float Height)
{
    /// <summary>Values closer together than this are treated as equal. Guards against float drift accumulated across nested layout passes.</summary>
    public const float Epsilon = 0.001f;

    public static Size Zero { get; } = new(0, 0);

    /// <summary>The largest space an element may be offered. Used by measurement probes that need an unbounded axis.</summary>
    public static Size Max { get; } = new(14_400, 14_400);

    public bool IsNegative => Width < -Epsilon || Height < -Epsilon;

    public Size WithWidth(float width) => new(width, Height);

    public Size WithHeight(float height) => new(Width, height);

    /// <summary>True when this size fits inside <paramref name="available"/>, tolerating sub-epsilon overshoot.</summary>
    public bool FitsIn(Size available) =>
        Width <= available.Width + Epsilon &&
        Height <= available.Height + Epsilon;

    public override string ToString() => $"(Width: {Width:F3}, Height: {Height:F3})";
}
