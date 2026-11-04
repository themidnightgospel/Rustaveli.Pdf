namespace Rustaveli.Pdf;

/// <summary>Keeps a number within bounds, as <c>Math.Clamp</c> would; netstandard2.0 has none.</summary>
internal static class Clamp
{
    /// <summary>
    /// <paramref name="value"/>, raised to <paramref name="smallest"/> or lowered to <paramref name="largest"/>.
    /// </summary>
    public static float Between(float value, float smallest, float largest) =>
        Math.Min(largest, Math.Max(smallest, value));

    /// <summary><paramref name="value"/>, kept from 0 to 1.</summary>
    public static float Fraction(float value) => Between(value, 0, 1);

    /// <summary>A fraction, kept from 0 to 1, as the nearest of the 256 steps of a byte.</summary>
    public static byte ToByte(float fraction) => (byte)Math.Round(Fraction(fraction) * 255);
}
