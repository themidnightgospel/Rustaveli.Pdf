namespace Rustaveli.Pdf;

/// <summary>
/// Checks the numbers a caller gives as they are given, rather than when the pages are laid out and drawn, where a NaN
/// or an infinity surfaces as a failure far from the code that passed it. Each is a number a PDF can write: finite, and
/// below 10^15 in magnitude.
/// </summary>
internal static class Numbers
{
    /// <summary><paramref name="value"/>, a number that may go either way, such as a shift or a turn.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is NaN, or 10^15 or more either way.</exception>
    public static float AnyWay(float value, string name) =>
        Writable.Is(value)
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "Must be a number less than 10^15 either way, the largest a PDF can write.");

    /// <summary><paramref name="value"/>, a length or a size: zero or more.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is negative, NaN, or 10^15 or more.</exception>
    public static float NotNegative(float value, string name) =>
        value >= 0 && Writable.Is(value)
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "Must be zero or more, and less than 10^15, the largest number a PDF can write.");

    /// <summary><paramref name="value"/>, a ratio or a factor that is nothing at zero: more than zero.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is zero, negative, NaN, or 10^15 or more.</exception>
    public static float Positive(float value, string name) =>
        value > 0 && Writable.Is(value)
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "Must be more than zero, and less than 10^15, the largest number a PDF can write.");

    /// <summary><paramref name="value"/>, a scale that may flip but not collapse: anything but zero.</summary>
    /// <exception cref="ArgumentOutOfRangeException">It is zero, NaN, or 10^15 or more either way.</exception>
    public static float NotZero(float value, string name) =>
        value != 0 && Writable.Is(value)
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "Must be a number other than zero, less than 10^15 either way, the largest a PDF can write.");
}
