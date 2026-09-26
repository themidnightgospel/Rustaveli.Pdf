using System.Text;

namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>
/// Builds PNG files chunk by chunk, so a test can state exactly the structure it exercises. Its CRC, zlib framing and
/// row filters are written out naively here, independently of the production code they check.
/// </summary>
internal static class TestPng
{
    public static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static byte[] Build(params byte[][] chunks)
    {
        using MemoryStream stream = new MemoryStream();
        stream.Write(Signature, 0, Signature.Length);
        foreach (byte[] chunk in chunks)
            stream.Write(chunk, 0, chunk.Length);
        return stream.ToArray();
    }

    /// <summary>A complete PNG: IHDR, the given chunks, the image data in one IDAT, IEND.</summary>
    public static byte[] Image(
        int width, int height, int bitDepth, int colorType, byte[] filteredRows, bool interlaced = false,
        params byte[][] beforeData) =>
        Build([Header(width, height, bitDepth, colorType, interlaced ? 1 : 0), .. beforeData,
            Chunk("IDAT", TestZlib.Compress(filteredRows)), End()]);

    public static byte[] Chunk(string type, byte[] data) => Chunk(type, data, fixCrc: true);

    public static byte[] Chunk(string type, byte[] data, bool fixCrc)
    {
        byte[] chunk = new byte[data.Length + 12];
        WriteUInt32(chunk, 0, (uint)data.Length);
        Encoding.ASCII.GetBytes(type, 0, 4, chunk, 4);
        data.CopyTo(chunk, 8);
        uint crc = Crc(chunk.AsSpan(4, data.Length + 4));
        WriteUInt32(chunk, data.Length + 8, fixCrc ? crc : ~crc);
        return chunk;
    }

    public static byte[] Header(int width, int height, int bitDepth, int colorType, int interlace = 0) =>
        Chunk("IHDR", HeaderData(width, height, bitDepth, colorType, interlace));

    public static byte[] HeaderData(int width, int height, int bitDepth, int colorType, int interlace = 0,
        int compression = 0, int filter = 0)
    {
        byte[] data = new byte[13];
        WriteUInt32(data, 0, (uint)width);
        WriteUInt32(data, 4, (uint)height);
        data[8] = (byte)bitDepth;
        data[9] = (byte)colorType;
        data[10] = (byte)compression;
        data[11] = (byte)filter;
        data[12] = (byte)interlace;
        return data;
    }

    public static byte[] End() => Chunk("IEND", []);

    public static byte[] Data(byte[] zlib) => Chunk("IDAT", zlib);

    /// <summary>Filters every row with <paramref name="filter"/> and prefixes the filter-type bytes.</summary>
    public static byte[] Filter(byte[][] rows, int step, byte filter) =>
        Filter(rows, step, rows.Select(_ => filter).ToArray());

    /// <summary>Filters row <c>i</c> with <paramref name="filters"/>[i] and prefixes the filter-type bytes.</summary>
    public static byte[] Filter(byte[][] rows, int step, byte[] filters)
    {
        using MemoryStream stream = new MemoryStream();
        byte[] previous = new byte[rows[0].Length];
        for (int y = 0; y < rows.Length; y++)
        {
            byte[] row = rows[y];
            stream.WriteByte(filters[y]);
            for (int x = 0; x < row.Length; x++)
            {
                int left = x >= step ? row[x - step] : 0;
                int above = previous[x];
                int upperLeft = x >= step ? previous[x - step] : 0;
                int prediction = filters[y] switch
                {
                    0 => 0,
                    1 => left,
                    2 => above,
                    3 => (left + above) / 2,
                    4 => Paeth(left, above, upperLeft),
                    _ => 0,
                };
                stream.WriteByte((byte)(row[x] - prediction));
            }

            previous = row;
        }

        return stream.ToArray();
    }

    public static int Paeth(int left, int above, int upperLeft)
    {
        int estimate = left + above - upperLeft;
        int a = Math.Abs(estimate - left);
        int b = Math.Abs(estimate - above);
        int c = Math.Abs(estimate - upperLeft);
        return a <= b && a <= c ? left : b <= c ? above : upperLeft;
    }

    /// <summary>Plain bit-by-bit CRC-32, deliberately unlike the table-driven production version.</summary>
    public static uint Crc(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }

        return ~crc;
    }

    /// <summary>Rewrites the CRC of every chunk, so that damage inside a chunk reaches the code behind the check.</summary>
    public static byte[] RepairCrcs(byte[] png)
    {
        byte[] copy = (byte[])png.Clone();
        int position = 8;
        while (position + 12 <= copy.Length)
        {
            uint length = ReadUInt32(copy, position);
            if (length > (uint)(copy.Length - position - 12))
                break;

            WriteUInt32(copy, position + 8 + (int)length, Crc(copy.AsSpan(position + 4, (int)length + 4)));
            position += 12 + (int)length;
        }

        return copy;
    }

    public static uint ReadUInt32(byte[] data, int offset) =>
        ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];

    public static void WriteUInt32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }

    public static byte[] BigEndian16(params int[] values)
    {
        byte[] data = new byte[values.Length * 2];
        for (int index = 0; index < values.Length; index++)
        {
            data[index * 2] = (byte)(values[index] >> 8);
            data[(index * 2) + 1] = (byte)values[index];
        }

        return data;
    }
}
