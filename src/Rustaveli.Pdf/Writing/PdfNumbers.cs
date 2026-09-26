using System.Buffers;
using System.Buffers.Text;

namespace Rustaveli.Pdf.Writing;

/// <summary>Formats numbers as PDF numeric tokens, culture-invariantly and without allocating.</summary>
internal static class PdfNumbers
{
    /// <summary>The most digits a real is written with after the decimal point.</summary>
    public const int MaxDecimals = 5;

    /// <summary>
    /// Reals at or beyond this magnitude are rejected. PDF forbids exponent notation, so a real is written out in
    /// full, and nothing a document legitimately contains comes near this size; a value that does is a defect upstream.
    /// </summary>
    public const double MaxRealMagnitude = 1e15;

    /// <summary>Room enough for any number this class writes: a sign, sixteen digits, a point and the decimals.</summary>
    public const int MaxLength = 24;

    private static readonly long[] PowersOfTen = [1, 10, 100, 1_000, 10_000, 100_000];

    /// <summary>Writes <paramref name="value"/> in decimal. <paramref name="destination"/> must hold <see cref="MaxLength"/> bytes.</summary>
    public static int WriteInteger(long value, Span<byte> destination)
    {
        _ = Utf8Formatter.TryFormat(value, destination, out int written);
        return written;
    }

    /// <summary>
    /// Writes <paramref name="value"/> in fixed-point notation. <paramref name="destination"/> must hold
    /// <see cref="MaxLength"/> bytes.
    /// </summary>
    /// <remarks>
    /// Precision is capped twice: at <see cref="MaxDecimals"/> decimals, and at seven significant digits for values of
    /// 100 and above. The second cap exists because the layout engine computes in <see cref="float"/>: 559.28f widened
    /// to double is 559.280029296875, and five decimals of that would write float noise — "559.28003" — into every
    /// coordinate. Seven significant digits sit just above a float's precision, so the noise rounds away and the
    /// value the author meant comes back. Trailing zeros are trimmed, and anything that rounds to zero is written
    /// "0", never "-0".
    /// </remarks>
    public static int WriteReal(double value, Span<byte> destination)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "PDF has no representation for NaN or infinity.");

        double magnitude = Math.Abs(value);
        if (magnitude >= MaxRealMagnitude)
            throw new ArgumentOutOfRangeException(nameof(value), value, "PDF output is limited to reals below 10^15 in magnitude.");

        int decimals = DecimalsFor(magnitude);
        long scaled = (long)Math.Round(magnitude * PowersOfTen[decimals], MidpointRounding.AwayFromZero);
        if (scaled == 0)
        {
            destination[0] = (byte)'0';
            return 1;
        }

        while (decimals > 0 && scaled % 10 == 0)
        {
            scaled /= 10;
            decimals--;
        }

        int length = 0;
        if (value < 0)
            destination[length++] = (byte)'-';

        long divisor = PowersOfTen[decimals];
        _ = Utf8Formatter.TryFormat(scaled / divisor, destination.Slice(length), out int written);
        length += written;

        if (decimals > 0)
        {
            destination[length++] = (byte)'.';
            _ = Utf8Formatter.TryFormat(scaled % divisor, destination.Slice(length), out written, new StandardFormat('D', (byte)decimals));
            length += written;
        }

        return length;
    }

    // Seven significant digits, but never more than MaxDecimals after the point.
    private static int DecimalsFor(double magnitude) => magnitude switch
    {
        < 100 => 5,
        < 1_000 => 4,
        < 10_000 => 3,
        < 100_000 => 2,
        < 1_000_000 => 1,
        _ => 0,
    };
}
