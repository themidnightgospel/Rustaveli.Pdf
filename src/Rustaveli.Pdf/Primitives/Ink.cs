using System.Globalization;

namespace Rustaveli.Pdf.Primitives;

/// <summary>
/// A colour as print thinks of it: an RGB colour, a CMYK process colour, or a named spot ink with a process-colour
/// fallback — at some tint and opacity (docs/adr/0004-ink-colour-model.md).
/// </summary>
/// <remarks>
/// Components are fractions from 0 to 1. A tint is a percentage of an ink, as a press lays down less of it: a tint of
/// an RGB colour moves it toward white paper, a tint of a process colour scales each component, and a tint of a spot
/// ink is recorded as the tint the plate prints at. Opacity is separate, and describes how much of what lies beneath
/// shows through.
/// </remarks>
public readonly struct Ink : IEquatable<Ink>
{
    // RGB: red, green, blue. CMYK: cyan, magenta, yellow, black. Spot: the fallback's components, in its own model.
    private readonly float _first;
    private readonly float _second;
    private readonly float _third;
    private readonly float _fourth;

    private Ink(InkModel model, float first, float second, float third, float fourth, float opacity, string? spotName, InkModel fallbackModel, float spotTint)
    {
        Model = model;
        _first = first;
        _second = second;
        _third = third;
        _fourth = fourth;
        Opacity = opacity;
        SpotName = spotName;
        FallbackModel = fallbackModel;
        SpotTint = spotTint;
    }

    /// <summary>Black ink alone: what black text and rules should print with.</summary>
    public static Ink Black { get; } = Cmyk(0, 0, 0, 1);

    /// <summary>No ink at all: the paper shows.</summary>
    public static Ink White { get; } = Cmyk(0, 0, 0, 0);

    /// <summary>Nothing is drawn.</summary>
    public static Ink Transparent { get; } = Rgb(0, 0, 0).WithOpacity(0);

    /// <summary>Prints on every separation, for crop and registration marks.</summary>
    public static Ink Registration { get; } = Spot("All", Cmyk(1, 1, 1, 1));

    public InkModel Model { get; }

    /// <summary>From 0, invisible, to 1, fully covering what lies beneath.</summary>
    public float Opacity { get; }

    public bool IsTransparent => Opacity == 0;

    /// <summary>The spot ink's name, or null for a process colour.</summary>
    public string? SpotName { get; }

    /// <summary>For a spot ink, the model its fallback is specified in; otherwise the ink's own model.</summary>
    public InkModel FallbackModel { get; }

    /// <summary>For a spot ink, the tint its plate prints at, from 0 to 1; otherwise 1.</summary>
    public float SpotTint { get; }

    public static Ink Rgb(byte red, byte green, byte blue) =>
        new Ink(InkModel.Rgb, red / 255f, green / 255f, blue / 255f, 0, 1, null, InkModel.Rgb, 1);

    public static Ink Cmyk(float cyan, float magenta, float yellow, float black)
    {
        RequireFraction(cyan, nameof(cyan));
        RequireFraction(magenta, nameof(magenta));
        RequireFraction(yellow, nameof(yellow));
        RequireFraction(black, nameof(black));

        return new Ink(InkModel.Cmyk, cyan, magenta, yellow, black, 1, null, InkModel.Cmyk, 1);
    }

    /// <summary>A named spot ink. The fallback is what devices that cannot print the spot ink use instead.</summary>
    public static Ink Spot(string name, Ink fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (fallback.Model == InkModel.Spot)
            throw new ArgumentException("A spot ink's fallback must be an RGB or CMYK colour, not another spot ink.", nameof(fallback));

        return new Ink(InkModel.Spot, fallback._first, fallback._second, fallback._third, fallback._fourth, fallback.Opacity, name, fallback.Model, 1);
    }

    /// <summary>
    /// Parses <c>#RGB</c>, <c>#ARGB</c>, <c>#RRGGBB</c> or <c>#AARRGGBB</c> as an RGB ink. The leading hash is optional;
    /// an alpha channel becomes the ink's opacity.
    /// </summary>
    public static Ink Hex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        string value = hex.StartsWith('#') ? hex.Substring(1) : hex;

        // Shorthand forms repeat each nibble, so #ABC and #AABBCC denote the same colour.
        value = value.Length switch
        {
            3 or 4 => string.Concat(value.Select(digit => new string(digit, 2))),
            6 or 8 => value,
            _ => throw new FormatException($"'{hex}' is not a valid colour. Expected 3, 4, 6 or 8 hexadecimal digits.")
        };

        // HexNumber would also allow surrounding whitespace, letting a space pad a short value out to a valid
        // length: "#12345 " would parse as #012345 instead of being rejected.
        if (!uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint parsed))
            throw new FormatException($"'{hex}' is not a valid colour. Expected hexadecimal digits.");

        // Six digits carry no alpha channel, so the ink is fully opaque.
        byte alpha = value.Length == 6 ? (byte)255 : (byte)(parsed >> 24);

        return Rgb((byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed).WithOpacity(alpha / 255f);
    }

    /// <summary>A percentage of this ink, from 0 (none: bare paper) to 1 (the ink itself).</summary>
    public Ink Tint(float amount)
    {
        RequireFraction(amount, nameof(amount));

        return Model switch
        {
            InkModel.Rgb => new Ink(InkModel.Rgb, TowardPaper(_first, amount), TowardPaper(_second, amount), TowardPaper(_third, amount), 0, Opacity, null, InkModel.Rgb, 1),
            InkModel.Cmyk => new Ink(InkModel.Cmyk, _first * amount, _second * amount, _third * amount, _fourth * amount, Opacity, null, InkModel.Cmyk, 1),
            _ => new Ink(InkModel.Spot, _first, _second, _third, _fourth, Opacity, SpotName, FallbackModel, SpotTint * amount)
        };
    }

    public Ink WithOpacity(float opacity)
    {
        RequireFraction(opacity, nameof(opacity));

        return new Ink(Model, _first, _second, _third, _fourth, opacity, SpotName, FallbackModel, SpotTint);
    }

    /// <summary>
    /// The colour as red, green and blue fractions, for output that can only show RGB. Process colours convert
    /// without a colour profile; a spot ink shows its fallback at the spot ink's tint.
    /// </summary>
    public (float Red, float Green, float Blue) ToRgb()
    {
        (float red, float green, float blue) = FallbackModel == InkModel.Rgb
            ? (_first, _second, _third)
            : ((1 - _first) * (1 - _fourth), (1 - _second) * (1 - _fourth), (1 - _third) * (1 - _fourth));

        if (Model != InkModel.Spot)
            return (red, green, blue);

        return (TowardPaper(red, SpotTint), TowardPaper(green, SpotTint), TowardPaper(blue, SpotTint));
    }

    /// <summary>
    /// The colour as cyan, magenta, yellow and black fractions. RGB converts without a colour profile, with black
    /// generated from the darkest channel; a spot ink gives its fallback at the spot ink's tint.
    /// </summary>
    public (float Cyan, float Magenta, float Yellow, float Black) ToCmyk()
    {
        (float cyan, float magenta, float yellow, float black) = FallbackModel == InkModel.Cmyk
            ? (_first, _second, _third, _fourth)
            : FromRgb(_first, _second, _third);

        if (Model != InkModel.Spot)
            return (cyan, magenta, yellow, black);

        return (cyan * SpotTint, magenta * SpotTint, yellow * SpotTint, black * SpotTint);
    }

    public bool Equals(Ink other) =>
        Model == other.Model
        && _first.Equals(other._first)
        && _second.Equals(other._second)
        && _third.Equals(other._third)
        && _fourth.Equals(other._fourth)
        && Opacity.Equals(other.Opacity)
        && string.Equals(SpotName, other.SpotName, StringComparison.Ordinal)
        && FallbackModel == other.FallbackModel
        && SpotTint.Equals(other.SpotTint);

    public override bool Equals(object? obj) => obj is Ink other && Equals(other);

    public override int GetHashCode()
    {
        int hash = (int)Model;
        hash = (hash * 397) ^ _first.GetHashCode();
        hash = (hash * 397) ^ _second.GetHashCode();
        hash = (hash * 397) ^ _third.GetHashCode();
        hash = (hash * 397) ^ _fourth.GetHashCode();
        hash = (hash * 397) ^ Opacity.GetHashCode();
        hash = (hash * 397) ^ (SpotName is null ? 0 : StringComparer.Ordinal.GetHashCode(SpotName));
        return (hash * 397) ^ SpotTint.GetHashCode();
    }

    public override string ToString()
    {
        string colour = Model switch
        {
            InkModel.Rgb => $"#{Byte(_first):X2}{Byte(_second):X2}{Byte(_third):X2}",
            InkModel.Cmyk => $"cmyk({Percent(_first)}, {Percent(_second)}, {Percent(_third)}, {Percent(_fourth)})",
            _ => $"spot '{SpotName}' {Percent(SpotTint)}"
        };

        return Opacity < 1 ? $"{colour} at {Percent(Opacity)} opacity" : colour;
    }

    public static bool operator ==(Ink left, Ink right) => left.Equals(right);

    public static bool operator !=(Ink left, Ink right) => !left.Equals(right);

    // Written so a full tint returns the component untouched: 1 - (1 - c) does not round-trip in floating point.
    private static float TowardPaper(float component, float amount) => component + ((1 - amount) * (1 - component));

    private static (float Cyan, float Magenta, float Yellow, float Black) FromRgb(float red, float green, float blue)
    {
        float black = 1 - Math.Max(red, Math.Max(green, blue));

        // Pure black has no chromatic part; dividing by the remaining lightness would divide by zero.
        if (black >= 1)
            return (0, 0, 0, 1);

        float lightness = 1 - black;
        return ((lightness - red) / lightness, (lightness - green) / lightness, (lightness - blue) / lightness, black);
    }

    private static byte Byte(float fraction) => (byte)Math.Round(fraction * 255);

    private static string Percent(float fraction) => (fraction * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static void RequireFraction(float value, string name)
    {
        if (!(value >= 0 && value <= 1))
            throw new ArgumentOutOfRangeException(name, value, "Must be a fraction from 0 to 1.");
    }
}
