namespace Rustaveli.Pdf;

/// <summary>
/// Checks the numbers a caller gives as they are given, rather than when the pages are laid out and drawn, where a NaN
/// or an infinity surfaces as a failure far from the code that passed it. A shift, a length, a factor or a scale is a
/// number a PDF can write: finite, and below 10^15 in magnitude.
/// </summary>
internal static class Numbers
{
    /// <summary>Whether <paramref name="value"/> is neither NaN nor an infinity.</summary>
    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <inheritdoc cref="IsFinite(float)"/>
    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    /// <summary><paramref name="value"/>, a resolution in pixels per inch: more than zero, and finite.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is zero, negative, NaN or infinite.</exception>
    public static float Resolution(float value, string name) =>
        value > 0 && IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(
                name, value, "A resolution is a finite number of pixels per inch above nothing.");

    /// <summary><paramref name="value"/>, the quality an image is compressed at: from 1 to 100.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is below 1 or above 100.</exception>
    public static int Quality(int value, string name) =>
        value is >= 1 and <= 100
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "Quality runs from 1 to 100.");

    /// <summary><paramref name="value"/>, a number that may go either way, such as a shift or a turn.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is NaN, or 10^15 or more either way.</exception>
    public static float AnyWay(float value, string name) =>
        Writable.Is(value)
            ? value
            : throw new ArgumentOutOfRangeException(
                name, value, "Must be a number less than 10^15 either way, the largest a PDF can write.");

    /// <summary><paramref name="value"/>, a length or a size: zero or more.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is negative, NaN, or 10^15 or more.</exception>
    public static float NotNegative(float value, string name) =>
        value >= 0 && Writable.Is(value)
            ? value
            : throw new ArgumentOutOfRangeException(
                name, value, "Must be zero or more, and less than 10^15, the largest number a PDF can write.");

    /// <summary><paramref name="value"/>, a ratio or a factor that is nothing at zero: more than zero.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is zero, negative, NaN, or 10^15 or more.</exception>
    public static float Positive(float value, string name) =>
        value > 0 && Writable.Is(value)
            ? value
            : throw new ArgumentOutOfRangeException(
                name, value, "Must be more than zero, and less than 10^15, the largest number a PDF can write.");

    /// <summary><paramref name="value"/>, a scale that may flip but not collapse: anything but zero.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is zero, NaN, or 10^15 or more either way.</exception>
    public static float NotZero(float value, string name) =>
        value != 0 && Writable.Is(value)
            ? value
            : throw new ArgumentOutOfRangeException(
                name,
                value,
                "Must be a number other than zero, less than 10^15 either way, the largest a PDF can write.");
}
