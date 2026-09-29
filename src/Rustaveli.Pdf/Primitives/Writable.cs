using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf;

/// <summary>
/// Whether numbers can be written to a PDF: finite, and below <see cref="PdfNumbers.MaxRealMagnitude"/> in magnitude.
/// Numbers read from outside, and what is built from them, can reach beyond that however they were meant, so they are
/// checked with this before they are drawn.
/// </summary>
internal static class Writable
{
    // NaN compares false with everything, so it is not writable either.
    public static bool Is(float value) => Math.Abs(value) < PdfNumbers.MaxRealMagnitude;

    public static bool Is(double value) => Math.Abs(value) < PdfNumbers.MaxRealMagnitude;

    public static bool Is(Offset point) => Is(point.X) && Is(point.Y);
}
