namespace Rustaveli.Pdf;

/// <summary>
/// A linear blend of two or more inks along a line at an angle across the box it fills, evenly spaced or at the
/// positions given.
/// </summary>
/// <remarks>
/// At 0 degrees the blend runs from left to right, and the angle turns it clockwise: 90 runs from top to bottom. As
/// in CSS, the line is long enough that the box's corners take the first and last inks exactly, whatever the angle.
/// </remarks>
public sealed class Gradient
{
    private readonly Ink[] _inks;
    private readonly float[] _positions;

    /// <summary>
    /// Where the blend runs when it is not across the box at an angle: from and to points given either as fractions of
    /// the box or in the space it is drawn in, as SVG places a gradient.
    /// </summary>
    private readonly (Offset Start, Offset End, bool OfBox)? _line;

    /// <summary>A blend at <paramref name="angle"/> degrees through <paramref name="inks"/>, evenly spaced, in order.</summary>
    /// <exception cref="ArgumentException">
    /// Fewer than two inks, inks of different opacity, or an angle that is not a finite number.
    /// </exception>
    public Gradient(float angle, params Ink[] inks)
        : this(angle, Evenly(inks ?? throw new ArgumentNullException(nameof(inks))))
    {
    }

    /// <summary>
    /// A blend at <paramref name="angle"/> degrees through <paramref name="stops"/>, each ink at its position along the
    /// line, from 0 at its start to 1 at its end.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Fewer than two stops, positions outside 0 to 1 or out of order, inks of different opacity, or an angle that is
    /// not a finite number.
    /// </exception>
    public Gradient(float angle, params GradientStop[] stops)
    {
        ArgumentNullException.ThrowIfNull(stops);

        if (float.IsNaN(angle) || float.IsInfinity(angle))
            throw new ArgumentException("A gradient's angle must be a finite number of degrees.", nameof(angle));

        Check(stops);

        // Opacity is applied to the whole blend at once, so it has to be one opacity.
        if (stops.Any(stop => stop.Ink.Opacity != stops[0].Ink.Opacity))
            throw new ArgumentException("The inks of a gradient must share one opacity.", nameof(stops));

        Angle = angle;
        _inks = stops.Select(stop => stop.Ink).ToArray();
        _positions = stops.Select(stop => stop.Position).ToArray();
    }

    private Gradient(Offset start, Offset end, bool ofBox, GradientStop[] stops, float opacity)
    {
        _inks = stops.Select(stop => stop.Ink.WithOpacity(opacity)).ToArray();
        _positions = stops.Select(stop => stop.Position).ToArray();
        _line = (start, end, ofBox);
    }

    /// <summary>The direction of the blend in degrees, clockwise from left to right.</summary>
    public float Angle { get; }

    /// <summary>The inks blended, first to last.</summary>
    public IReadOnlyList<Ink> Inks => _inks;

    /// <summary>Where each ink lies along the blend, from 0 at its start to 1 at its end.</summary>
    public IReadOnlyList<float> Positions => _positions;

    /// <summary>The opacity the whole blend is drawn at.</summary>
    public float Opacity => _inks[0].Opacity;

    /// <summary>A blend from left to right.</summary>
    public static Gradient Across(params Ink[] inks) => new Gradient(0, inks);

    /// <summary>A blend from top to bottom.</summary>
    public static Gradient Down(params Ink[] inks) => new Gradient(90, inks);

    /// <summary>
    /// A blend from <paramref name="start"/> to <paramref name="end"/>: fractions of the box it fills when
    /// <paramref name="ofBox"/>, otherwise points in the space it is drawn in. Inks of different opacity are drawn at
    /// their mean, since a blend is drawn at one opacity.
    /// </summary>
    internal static Gradient Between(Offset start, Offset end, bool ofBox, IReadOnlyList<GradientStop> stops)
    {
        GradientStop[] ordered = stops.ToArray();
        Check(ordered);
        return new Gradient(start, end, ofBox, ordered, ordered.Average(stop => stop.Ink.Opacity));
    }

    /// <summary>
    /// Where the blend starts and ends for a box at <paramref name="position"/> of <paramref name="size"/>: a line
    /// through the box's centre at the gradient's angle, reaching the corners' perpendiculars at both ends, unless the
    /// gradient has a line of its own.
    /// </summary>
    internal (Offset Start, Offset End) Axis(Offset position, Extent size)
    {
        if (_line is { } line)
        {
            return line.OfBox
                ? (OnBox(line.Start), OnBox(line.End))
                : (line.Start, line.End);
        }

        double radians = Angle * Math.PI / 180;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        double reach = (Math.Abs(cos) * size.Width / 2) + (Math.Abs(sin) * size.Height / 2);
        Offset centre = new Offset(position.X + (size.Width / 2), position.Y + (size.Height / 2));
        Offset half = new Offset((float)(cos * reach), (float)(sin * reach));

        return (centre + half.Reverse(), centre + half);

        Offset OnBox(Offset fraction) =>
            new Offset(position.X + (fraction.X * size.Width), position.Y + (fraction.Y * size.Height));
    }

    private static GradientStop[] Evenly(Ink[] inks) =>
        inks.Select((ink, index) => new GradientStop(inks.Length < 2 ? 0 : (float)index / (inks.Length - 1), ink)).ToArray();

    private static void Check(GradientStop[] stops)
    {
        if (stops.Length < 2)
            throw new ArgumentException("A gradient blends at least two inks.", nameof(stops));

        for (int index = 0; index < stops.Length; index++)
        {
            float position = stops[index].Position;

            if (!(position >= 0 && position <= 1))
                throw new ArgumentException("A gradient stop lies from 0 at the start of the blend to 1 at its end.", nameof(stops));

            if (index > 0 && position < stops[index - 1].Position)
                throw new ArgumentException("Gradient stops are given in order along the blend.", nameof(stops));
        }
    }
}
