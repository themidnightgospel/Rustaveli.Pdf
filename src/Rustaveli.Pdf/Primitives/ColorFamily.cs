namespace Rustaveli.Pdf.Primitives;

/// <summary>
/// A family of related shades built around one base colour.
/// </summary>
/// <remarks>
/// Converts implicitly to its <see cref="Base"/> shade, so a family reads as a colour where one is expected
/// while the individual steps stay reachable.
/// </remarks>
public class ColorFamily
{
    internal ColorFamily(string @base, string lighten5, string lighten4, string lighten3, string lighten2,
        string lighten1, string darken1, string darken2, string darken3, string darken4)
    {
        Base = Color.ParseHex(@base);
        Lighten5 = Color.ParseHex(lighten5);
        Lighten4 = Color.ParseHex(lighten4);
        Lighten3 = Color.ParseHex(lighten3);
        Lighten2 = Color.ParseHex(lighten2);
        Lighten1 = Color.ParseHex(lighten1);
        Darken1 = Color.ParseHex(darken1);
        Darken2 = Color.ParseHex(darken2);
        Darken3 = Color.ParseHex(darken3);
        Darken4 = Color.ParseHex(darken4);
    }

    /// <summary>The reference shade, Material's 500.</summary>
    public Color Base { get; }

    /// <summary>The palest shade, Material's 50.</summary>
    public Color Lighten5 { get; }

    public Color Lighten4 { get; }

    public Color Lighten3 { get; }

    public Color Lighten2 { get; }

    public Color Lighten1 { get; }

    public Color Darken1 { get; }

    public Color Darken2 { get; }

    public Color Darken3 { get; }

    /// <summary>The deepest shade, Material's 900.</summary>
    public Color Darken4 { get; }

    /// <summary>
    /// Converts a family to its base shade.
    /// </summary>
    /// <remarks>
    /// A family is a lookup table, not a colour value. C# applies this conversion to a conditional expression
    /// as a whole rather than to each arm, so <c>flag ? Colors.Red : null</c> converts a null family and fails.
    /// Write <c>flag ? Colors.Red.Base : null</c> when an arm may be null.
    /// </remarks>
    public static implicit operator Color(ColorFamily family)
    {
        ArgumentNullException.ThrowIfNull(family);

        return family.Base;
    }

    /// <summary>Compares against a colour by value, and against another family by identity.</summary>
    public override bool Equals(object? obj) => obj is Color color ? Base.Equals(color) : ReferenceEquals(this, obj);

    public bool Equals(Color other) => Base.Equals(other);

    public override int GetHashCode() => Base.GetHashCode();

    public override string ToString() => Base.ToString();
}
