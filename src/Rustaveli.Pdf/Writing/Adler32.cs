namespace Rustaveli.Pdf.Writing;

/// <summary>The Adler-32 checksum that closes a zlib stream (RFC 1950, 9).</summary>
internal static class Adler32
{
    private const uint Modulus = 65521;

    /// <summary>
    /// The most bytes that can be summed before reducing without the 32-bit sums overflowing, even when every byte is
    /// 0xFF and both sums start just below the modulus. Reducing once per block instead of once per byte is what
    /// makes the checksum cheap.
    /// </summary>
    private const int BlockLength = 5552;

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint low = 1;
        uint high = 0;

        while (data.Length > 0)
        {
            int count = Math.Min(data.Length, BlockLength);
            foreach (byte value in data.Slice(0, count))
            {
                low += value;
                high += low;
            }

            low %= Modulus;
            high %= Modulus;
            data = data.Slice(count);
        }

        return (high << 16) | low;
    }
}
