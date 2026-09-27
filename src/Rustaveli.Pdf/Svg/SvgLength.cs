using System.Globalization;

namespace Rustaveli.Pdf.Svg;

/// <summary>
/// Reads SVG lengths into user units, which are CSS pixels: a pixel is three quarters of a point, a percentage a
/// share of a reference length.
/// </summary>
internal static class SvgLength
{
    /// <summary>Points in a CSS pixel.</summary>
    public const float PointsPerPixel = 0.75f;

    /// <summary>
    /// The length <paramref name="text"/> in user units, a percentage taken of <paramref name="reference"/>, or
    /// <paramref name="fallback"/> when there is no length to read.
    /// </summary>
    public static float Read(string? text, float reference, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        string value = text!.Trim();
        int end = 0;

        while (end < value.Length && (char.IsDigit(value[end]) || value[end] is '.' or '-' or '+' or 'e' or 'E'))
        {
            // An "e" starts an "em" unit unless a digit or sign follows it.
            if (value[end] is 'e' or 'E' && (end + 1 >= value.Length || !(char.IsDigit(value[end + 1]) || value[end + 1] is '-' or '+')))
                break;

            end++;
        }

        if (!float.TryParse(value.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
            return fallback;

        return value.Substring(end).Trim().ToLowerInvariant() switch
        {
            "" or "px" => number,
            "pt" => number / PointsPerPixel,
            "pc" => number * 16,
            "mm" => number * 96 / 25.4f,
            "cm" => number * 96 / 2.54f,
            "in" => number * 96,
            "em" => number * 16,
            "ex" => number * 8,
            "%" => number / 100 * reference,
            _ => number,
        };
    }

    /// <summary>A fraction for a gradient's point: a plain number, or a percentage of one.</summary>
    public static float Fraction(string? text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        string value = text!.Trim();
        bool percent = value.EndsWith("%", StringComparison.Ordinal);

        return float.TryParse(percent ? value.Substring(0, value.Length - 1) : value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
            ? (percent ? number / 100 : number)
            : fallback;
    }
}
