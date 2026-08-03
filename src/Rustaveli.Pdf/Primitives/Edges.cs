namespace Rustaveli.Pdf.Primitives;

/// <summary>
/// Per-side lengths in PDF points, used for padding and border widths.
/// </summary>
public readonly record struct Edges(float Left, float Top, float Right, float Bottom)
{
    public static Edges Zero { get; } = new(0, 0, 0, 0);

    public static Edges All(float value) => new(value, value, value, value);

    public static Edges Symmetric(float horizontal, float vertical) =>
        new(horizontal, vertical, horizontal, vertical);

    public float Horizontal => Left + Right;

    public float Vertical => Top + Bottom;

    public Edges WithLeft(float value) => this with { Left = value };

    public Edges WithTop(float value) => this with { Top = value };

    public Edges WithRight(float value) => this with { Right = value };

    public Edges WithBottom(float value) => this with { Bottom = value };
}
