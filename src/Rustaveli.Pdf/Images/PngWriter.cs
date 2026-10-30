using System.Buffers.Binary;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// Writes eight-bit RGB pixels as a PNG: the signature, a header, one zlib-framed deflate stream of unfiltered rows,
/// and the end, each chunk closed by its CRC.
/// </summary>
internal static class PngWriter
{
    /// <summary>A PNG of <paramref name="width"/> by <paramref name="height"/> pixels, three bytes each, a row at a time.</summary>
    public static byte[] Rgb(int width, int height, byte[] pixels)
    {
        int rowBytes = width * 3;
        int expected = rowBytes * height;

        if (pixels.Length != expected)
        {
            throw new ArgumentException(
                $"{width} by {height} pixels take {expected} bytes, not {pixels.Length}.", nameof(pixels));
        }

        using MemoryStream png = new MemoryStream();
        png.Write(ImageFormatDetector.PngSignature);

        byte[] header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height);
        header[8] = 8;
        header[9] = 2;
        Chunk(png, "IHDR"u8, header);

        // Each row starts with its filter, none, before its pixels.
        byte[] rows = new byte[height * (rowBytes + 1)];
        for (int row = 0; row < height; row++)
            Buffer.BlockCopy(pixels, row * rowBytes, rows, (row * (rowBytes + 1)) + 1, rowBytes);

        Chunk(png, "IDAT"u8, Zlib(rows));
        Chunk(png, "IEND"u8, []);

        return png.ToArray();
    }

    private static byte[] Zlib(byte[] data)
    {
        using ZlibWriter zlib = new ZlibWriter();
        zlib.Write(data, 0, data.Length);
        return zlib.Finish();
    }

    private static void Chunk(Stream png, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> field = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(field, (uint)data.Length);
        png.Write(field);
        png.Write(type);
        png.Write(data);

        BinaryPrimitives.WriteUInt32BigEndian(field, Crc32.Append(Crc32.Compute(type), data));
        png.Write(field);
    }
}
