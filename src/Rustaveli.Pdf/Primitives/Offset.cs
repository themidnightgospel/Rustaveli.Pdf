namespace Rustaveli.Pdf;

/// <summary>
/// A point, or a distance to move by, in PDF points: across from the left and down from the top, as layout measures
/// a page.
/// </summary>
public readonly record struct Offset(float X, float Y)
{
    /// <summary>The origin itself, or no move at all.</summary>
    public static Offset Zero { get; } = new Offset(0, 0);

    /// <summary>
    /// The move that undoes this one: as far, in the opposite direction on both axes. Moving by an offset and then by
    /// its reverse comes back to where it started.
    /// </summary>
    public Offset Reverse() => new Offset(-X, -Y);

    public static Offset operator +(Offset left, Offset right) => new Offset(left.X + right.X, left.Y + right.Y);
}
