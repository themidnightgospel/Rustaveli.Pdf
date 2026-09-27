namespace Rustaveli.Pdf.Images;

/// <summary>
/// The Adler-32 checksum that closes every zlib stream (RFC 1950), computed incrementally.
/// </summary>
internal static class Adler32
{
    /// <summary>The checksum of no data, and the value to start from.</summary>
    public const uint Initial = 1;

    private const uint Modulus = 65521;

    // The largest number of bytes that can be summed before the 32-bit accumulators could overflow, so the modulo
    // is taken once per block rather than once per byte (the same bound zlib uses).
    private const int BlockSize = 5552;

    public static uint Append(uint adler, ReadOnlySpan<byte> data)
    {
        uint low = adler & 0xFFFF;
        uint high = adler >> 16;

        while (data.Length > 0)
        {
            int block = Math.Min(data.Length, BlockSize);
            for (int index = 0; index < block; index++)
            {
                low += data[index];
                high += low;
            }

            low %= Modulus;
            high %= Modulus;
            data = data.Slice(block);
        }

        return (high << 16) | low;
    }
}
