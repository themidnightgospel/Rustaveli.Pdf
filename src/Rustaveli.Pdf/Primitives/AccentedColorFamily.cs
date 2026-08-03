namespace Rustaveli.Pdf.Primitives;

/// <summary>
/// A colour family that also carries the four saturated accent shades.
/// </summary>
/// <remarks>
/// The neutral families — grey, blue grey and brown — have no accents in Material, which is why they are a
/// plain <see cref="ColorFamily"/>.
/// </remarks>
public sealed class AccentedColorFamily : ColorFamily
{
    internal AccentedColorFamily(string @base, string lighten5, string lighten4, string lighten3, string lighten2,
        string lighten1, string darken1, string darken2, string darken3, string darken4,
        string accent1, string accent2, string accent3, string accent4)
        : base(@base, lighten5, lighten4, lighten3, lighten2, lighten1, darken1, darken2, darken3, darken4)
    {
        Accent1 = Color.ParseHex(accent1);
        Accent2 = Color.ParseHex(accent2);
        Accent3 = Color.ParseHex(accent3);
        Accent4 = Color.ParseHex(accent4);
    }

    public Color Accent1 { get; }

    public Color Accent2 { get; }

    public Color Accent3 { get; }

    public Color Accent4 { get; }
}
