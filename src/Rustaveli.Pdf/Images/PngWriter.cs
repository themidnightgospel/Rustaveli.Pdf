using System.IO.Compression;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// Writes eight-bit RGB pixels as a PNG: the signature, a header, one zlib-framed deflate stream of unfiltered rows,
/// and the end, each chunk closed by its CRC.
/// </summary>
internal static class PngWriter
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A PNG of <paramref name="width"/> by <paramref name="height"/> pixels, three bytes each, a row at a time.</summary>
    public static byte[] Rgb(int width, int height, byte[] pixels)
    {
        if (pixels.Length != width * height * 3)
            throw new ArgumentException($"{width} by {height} pixels take {width * height * 3} bytes, not {pixels.Length}.", nameof(pixels));

        using MemoryStream png = new MemoryStream();
        png.Write(Signature, 0, Signature.Length);

        byte[] header = new byte[13];
        BigEndian(header, 0, (uint)width);
        BigEndian(header, 4, (uint)height);
        header[8] = 8;
        header[9] = 2;
        Chunk(png, "IHDR", header);

        // Each row starts with its filter, none, before its pixels.
        byte[] rows = new byte[height * ((width * 3) + 1)];
        for (int row = 0; row < height; row++)
            Buffer.BlockCopy(pixels, row * width * 3, rows, (row * ((width * 3) + 1)) + 1, width * 3);

        Chunk(png, "IDAT", Zlib(rows));
        Chunk(png, "IEND", []);

        return png.ToArray();
    }

    private static byte[] Zlib(byte[] data)
    {
        using MemoryStream framed = new MemoryStream();
        framed.WriteByte(0x78);
        framed.WriteByte(0x9C);

        using (DeflateStream deflate = new DeflateStream(framed, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(data, 0, data.Length);

        byte[] checksum = new byte[4];
        BigEndian(checksum, 0, Adler32.Append(Adler32.Initial, data));
        framed.Write(checksum, 0, checksum.Length);

        return framed.ToArray();
    }

    private static void Chunk(Stream png, string type, byte[] data)
    {
        byte[] length = new byte[4];
        BigEndian(length, 0, (uint)data.Length);
        png.Write(length, 0, length.Length);

        byte[] typed = new byte[4 + data.Length];
        for (int index = 0; index < 4; index++)
            typed[index] = (byte)type[index];

        Buffer.BlockCopy(data, 0, typed, 4, data.Length);
        png.Write(typed, 0, typed.Length);

        byte[] crc = new byte[4];
        BigEndian(crc, 0, Crc32.Compute(typed));
        png.Write(crc, 0, crc.Length);
    }

    private static void BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
