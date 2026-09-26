using System.Globalization;

namespace Rustaveli.Pdf.Primitives;

/// <summary>
/// A straight (non-premultiplied) 8-bit-per-channel RGBA colour.
/// </summary>
public readonly record struct Color(byte Red, byte Green, byte Blue, byte Alpha = 255)
{
    public bool IsTransparent => Alpha == 0;

    public uint ToArgb() => ((uint)Alpha << 24) | ((uint)Red << 16) | ((uint)Green << 8) | Blue;

    public static Color FromArgb(uint argb) => new(
        (byte)((argb >> 16) & 0xFF),
        (byte)((argb >> 8) & 0xFF),
        (byte)(argb & 0xFF),
        (byte)((argb >> 24) & 0xFF));

    /// <summary>
    /// Parses <c>#RGB</c>, <c>#ARGB</c>, <c>#RRGGBB</c> or <c>#AARRGGBB</c>. The leading hash is optional.
    /// </summary>
    public static Color ParseHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);

        string value = hex.StartsWith('#') ? hex[1..] : hex;

        // Shorthand forms repeat each nibble, so #ABC and #AABBCC denote the same colour.
        value = value.Length switch
        {
            3 or 4 => string.Concat(value.Select(c => new string(c, 2))),
            6 or 8 => value,
            _ => throw new FormatException($"'{hex}' is not a valid colour. Expected 3, 4, 6 or 8 hexadecimal digits.")
        };

        // HexNumber would also allow surrounding whitespace, letting a space pad a short value out to a valid
        // length: "#12345 " would parse as #012345 instead of being rejected.
        if (!uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint parsed))
            throw new FormatException($"'{hex}' is not a valid colour. Expected hexadecimal digits.");

        // Six-digit input carries no alpha channel, so default it to fully opaque.
        if (value.Length == 6)
            parsed |= 0xFF000000;

        return FromArgb(parsed);
    }

    public Color WithAlpha(byte alpha) => this with { Alpha = alpha };

    public override string ToString() => $"#{Alpha:X2}{Red:X2}{Green:X2}{Blue:X2}";
}
