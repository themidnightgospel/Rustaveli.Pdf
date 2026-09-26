using System.IO.Compression;

namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>
/// zlib framing and PNG prediction written out naively, to build inputs and to read outputs independently of the
/// production code under test.
/// </summary>
internal static class TestZlib
{
    public static byte[] Compress(byte[] data, CompressionLevel level = CompressionLevel.Optimal)
    {
        using MemoryStream output = new MemoryStream();
        output.WriteByte(0x78);
        output.WriteByte(0x9C);
        using (DeflateStream deflate = new DeflateStream(output, level, leaveOpen: true))
            deflate.Write(data, 0, data.Length);

        uint adler = Adler(data);
        output.WriteByte((byte)(adler >> 24));
        output.WriteByte((byte)(adler >> 16));
        output.WriteByte((byte)(adler >> 8));
        output.WriteByte((byte)adler);
        return output.ToArray();
    }

    /// <summary>Inflates a zlib stream, checking its header and checksum.</summary>
    public static byte[] Decompress(ReadOnlyMemory<byte> zlib)
    {
        byte[] data = zlib.ToArray();
        Assert.Equal(0x78, data[0]);
        Assert.Equal(0, ((data[0] << 8) | data[1]) % 31);

        using MemoryStream input = new MemoryStream(data, 2, data.Length - 6);
        using DeflateStream inflate = new DeflateStream(input, CompressionMode.Decompress);
        using MemoryStream output = new MemoryStream();
        inflate.CopyTo(output);
        byte[] result = output.ToArray();

        uint stored = TestPng.ReadUInt32(data, data.Length - 4);
        Assert.Equal(Adler(result), stored);
        return result;
    }

    /// <summary>Reverses /Predictor 15: splits inflated data into rows and undoes each row's PNG filter.</summary>
    public static byte[][] Unpredict(byte[] data, int colors, int bitsPerComponent, int columns)
    {
        int rowBytes = ((columns * colors * bitsPerComponent) + 7) / 8;
        int step = Math.Max(1, colors * bitsPerComponent / 8);
        Assert.Equal(0, data.Length % (rowBytes + 1));

        List<byte[]> rows = new List<byte[]>();
        byte[] previous = new byte[rowBytes];
        for (int offset = 0; offset < data.Length; offset += rowBytes + 1)
        {
            byte filter = data[offset];
            byte[] row = new byte[rowBytes];
            for (int x = 0; x < rowBytes; x++)
            {
                int left = x >= step ? row[x - step] : 0;
                int above = previous[x];
                int upperLeft = x >= step ? previous[x - step] : 0;
                int prediction = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => above,
                    3 => (left + above) / 2,
                    4 => TestPng.Paeth(left, above, upperLeft),
                    _ => throw new InvalidDataException($"filter {filter}"),
                };
                row[x] = (byte)(data[offset + 1 + x] + prediction);
            }

            rows.Add(row);
            previous = row;
        }

        return rows.ToArray();
    }

    /// <summary>The filter-type byte of every row of inflated predicted data.</summary>
    public static byte[] RowFilters(byte[] data, int rowBytes) =>
        Enumerable.Range(0, data.Length / (rowBytes + 1)).Select(row => data[row * (rowBytes + 1)]).ToArray();

    public static uint Adler(byte[] data)
    {
        uint a = 1;
        uint b = 0;
        foreach (byte value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }
}
