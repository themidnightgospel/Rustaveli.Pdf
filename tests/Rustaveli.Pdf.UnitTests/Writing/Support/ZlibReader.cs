using System.IO.Compression;

namespace Rustaveli.Pdf.UnitTests.Writing;

/// <summary>
/// Decodes zlib data with the framework's inflater, checking the framing the writer adds around it: a valid
/// header and an Adler-32 trailer that matches the inflated bytes.
/// </summary>
internal static class ZlibReader
{
    public static byte[] Inflate(byte[] zlib)
    {
        if (zlib.Length < 6)
            throw new FormatException("Too short to be a zlib stream.");

        if ((zlib[0] & 0x0F) != 8 || (zlib[0] >> 4) > 7)
            throw new FormatException($"Header byte 0x{zlib[0]:X2} does not declare deflate with a window of at most 32 KiB.");

        if (((zlib[0] << 8) | zlib[1]) % 31 != 0)
            throw new FormatException("The header check bits are wrong.");

        if ((zlib[1] & 0x20) != 0)
            throw new FormatException("A preset dictionary is not allowed in PDF.");

        byte[] inflated;
        using (MemoryStream compressed = new MemoryStream(zlib, 2, zlib.Length - 6))
        using (DeflateStream deflate = new DeflateStream(compressed, CompressionMode.Decompress))
        using (MemoryStream output = new MemoryStream())
        {
            deflate.CopyTo(output);
            inflated = output.ToArray();
        }

        uint stored = ((uint)zlib[^4] << 24) | ((uint)zlib[^3] << 16) | ((uint)zlib[^2] << 8) | zlib[^1];
        uint actual = ReferenceAdler32(inflated);
        if (stored != actual)
            throw new FormatException($"Adler-32 trailer 0x{stored:X8} does not match the data's 0x{actual:X8}.");

        return inflated;
    }

    /// <summary>The checksum computed the slow, obviously correct way: reduce after every byte.</summary>
    public static uint ReferenceAdler32(ReadOnlySpan<byte> data)
    {
        uint low = 1;
        uint high = 0;
        foreach (byte value in data)
        {
            low = (low + value) % 65521;
            high = (high + low) % 65521;
        }

        return (high << 16) | low;
    }
}
