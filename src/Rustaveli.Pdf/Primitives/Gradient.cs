namespace Rustaveli.Pdf;

/// <summary>
/// A linear blend of two or more inks, evenly spaced along a line at an angle across the box it fills.
/// </summary>
/// <remarks>
/// At 0 degrees the blend runs from left to right, and the angle turns it clockwise: 90 runs from top to bottom. As
/// in CSS, the line is long enough that the box's corners take the first and last inks exactly, whatever the angle.
/// </remarks>
public sealed class Gradient
{
    private readonly Ink[] _inks;

    /// <summary>A blend at <paramref name="angle"/> degrees through <paramref name="inks"/>, in order.</summary>
    /// <exception cref="ArgumentException">
    /// Fewer than two inks, inks of different opacity, or an angle that is not a finite number.
    /// </exception>
    public Gradient(float angle, params Ink[] inks)
    {
        ArgumentNullException.ThrowIfNull(inks);

        if (float.IsNaN(angle) || float.IsInfinity(angle))
            throw new ArgumentException("A gradient's angle must be a finite number of degrees.", nameof(angle));

        if (inks.Length < 2)
            throw new ArgumentException("A gradient blends at least two inks.", nameof(inks));

        // Opacity is applied to the whole blend at once, so it has to be one opacity.
        if (inks.Any(ink => ink.Opacity != inks[0].Opacity))
            throw new ArgumentException("The inks of a gradient must share one opacity.", nameof(inks));

        Angle = angle;
        _inks = (Ink[])inks.Clone();
    }

    /// <summary>The direction of the blend in degrees, clockwise from left to right.</summary>
    public float Angle { get; }

    /// <summary>The inks blended, first to last.</summary>
    public IReadOnlyList<Ink> Inks => _inks;

    /// <summary>The opacity the whole blend is drawn at.</summary>
    public float Opacity => _inks[0].Opacity;

    /// <summary>A blend from left to right.</summary>
    public static Gradient Across(params Ink[] inks) => new Gradient(0, inks);

    /// <summary>A blend from top to bottom.</summary>
    public static Gradient Down(params Ink[] inks) => new Gradient(90, inks);

    /// <summary>
    /// Where the blend starts and ends for a box at <paramref name="position"/> of <paramref name="size"/>: a line
    /// through the box's centre at the gradient's angle, reaching the corners' perpendiculars at both ends.
    /// </summary>
    internal (Offset Start, Offset End) Axis(Offset position, Extent size)
    {
        double radians = Angle * Math.PI / 180;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        double reach = (Math.Abs(cos) * size.Width / 2) + (Math.Abs(sin) * size.Height / 2);
        Offset centre = new Offset(position.X + (size.Width / 2), position.Y + (size.Height / 2));
        Offset half = new Offset((float)(cos * reach), (float)(sin * reach));

        return (centre + half.Reverse(), centre + half);
    }
}
