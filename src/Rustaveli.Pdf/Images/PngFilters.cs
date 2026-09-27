namespace Rustaveli.Pdf.Images;

/// <summary>
/// The five PNG row filters. Each predicts a byte from its neighbours — a: the corresponding byte of the previous
/// pixel, b: the byte above, c: the byte above <c>a</c> — and stores the difference.
/// </summary>
internal static class PngFilters
{
    public const byte None = 0;
    public const byte Sub = 1;
    public const byte Up = 2;
    public const byte Average = 3;
    public const byte Paeth = 4;

    /// <summary>
    /// Reverses <paramref name="filter"/> on <paramref name="row"/> in place, given the already-reconstructed
    /// <paramref name="previous"/> row (all zeros for the first row of an image or pass).
    /// </summary>
    public static void Unfilter(byte filter, Span<byte> row, ReadOnlySpan<byte> previous, int step)
    {
        int length = row.Length;
        switch (filter)
        {
            case None:
                break;

            case Sub:
                for (int index = step; index < length; index++)
                    row[index] += row[index - step];
                break;

            case Up:
                for (int index = 0; index < length; index++)
                    row[index] += previous[index];
                break;

            // Every row holds at least one whole pixel, so it is never shorter than the step.
            case Average:
                for (int index = 0; index < step; index++)
                    row[index] += (byte)(previous[index] >> 1);
                for (int index = step; index < length; index++)
                    row[index] += (byte)((row[index - step] + previous[index]) >> 1);
                break;

            case Paeth:
                // With no pixel to the left, a and c are zero and the predictor reduces to b.
                for (int index = 0; index < step; index++)
                    row[index] += previous[index];
                for (int index = step; index < length; index++)
                    row[index] += Predict(row[index - step], previous[index], previous[index - step]);
                break;

            default:
                throw new ImageFormatException($"The PNG uses row filter type {filter}; only 0 to 4 exist.");
        }
    }

    /// <summary>
    /// Writes the Paeth-filtered form of <paramref name="row"/> into <paramref name="output"/>, which is one byte
    /// longer: the filter-type byte comes first.
    /// </summary>
    public static void ApplyPaeth(ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, Span<byte> output, int step)
    {
        output[0] = Paeth;
        int length = row.Length;
        for (int index = 0; index < step; index++)
            output[index + 1] = (byte)(row[index] - previous[index]);
        for (int index = step; index < length; index++)
        {
            byte prediction = Predict(row[index - step], previous[index], previous[index - step]);
            output[index + 1] = (byte)(row[index] - prediction);
        }
    }

    /// <summary>The Paeth predictor: whichever of a, b and c is closest to a + b − c, preferring a, then b.</summary>
    public static byte Predict(byte left, byte above, byte upperLeft)
    {
        int estimate = left + above - upperLeft;
        int distanceLeft = Math.Abs(estimate - left);
        int distanceAbove = Math.Abs(estimate - above);
        int distanceUpperLeft = Math.Abs(estimate - upperLeft);

        if (distanceLeft <= distanceAbove && distanceLeft <= distanceUpperLeft)
            return left;

        return distanceAbove <= distanceUpperLeft ? above : upperLeft;
    }
}
