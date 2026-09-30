using System.Globalization;

namespace Rustaveli.Pdf;

/// <summary>
/// Writes a length in points as sizes and positions describe themselves in messages: to a thousandth of a point at
/// most, with no zeros trailing, and the same whatever culture the program runs under.
/// </summary>
/// <remarks>
/// A thousandth of a point is the tolerance layout compares lengths within, so finer digits would only show drift.
/// A length that rounds to nothing is written as 0, never as -0.
/// </remarks>
internal static class PointText
{
    public static string Of(float points)
    {
        double rounded = Math.Round(points, 3, MidpointRounding.AwayFromZero);
        return (rounded == 0 ? 0d : rounded).ToString("0.###", CultureInfo.InvariantCulture);
    }
}
