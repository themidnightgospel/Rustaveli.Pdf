namespace Rustaveli.Pdf.Primitives;

/// <summary>
/// Per-side lengths in PDF points, used for padding and border widths.
/// </summary>
public readonly record struct Sides(float Left, float Top, float Right, float Bottom)
{
    public static Sides Zero { get; } = new(0, 0, 0, 0);

    public static Sides All(float value) => new(value, value, value, value);

    public static Sides Symmetric(float horizontal, float vertical) =>
        new(horizontal, vertical, horizontal, vertical);

    public float Horizontal => Left + Right;

    public float Vertical => Top + Bottom;

    public Sides WithLeft(float value) => this with { Left = value };

    public Sides WithTop(float value) => this with { Top = value };

    public Sides WithRight(float value) => this with { Right = value };

    public Sides WithBottom(float value) => this with { Bottom = value };
}
