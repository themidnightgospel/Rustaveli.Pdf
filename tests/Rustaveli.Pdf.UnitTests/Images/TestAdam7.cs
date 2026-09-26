namespace Rustaveli.Pdf.UnitTests.Images;

/// <summary>Interlaces rows the way a PNG encoder does, written out plainly for tests.</summary>
internal static class TestAdam7
{
    private static readonly (int X, int Y, int StepX, int StepY)[] Passes =
        [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)];

    /// <summary>
    /// The filtered image data of <paramref name="rows"/> interlaced: each non-empty pass extracted, packed and
    /// filtered as an image of its own, the filter for each of its rows taken in turn from <paramref name="filters"/>.
    /// </summary>
    public static byte[] Interlace(byte[][] rows, int width, int bitsPerPixel, IEnumerable<byte> filters)
    {
        using IEnumerator<byte> filter = filters.GetEnumerator();
        List<byte> data = [];
        int height = rows.Length;
        int step = Math.Max(1, bitsPerPixel / 8);

        foreach ((int startX, int startY, int stepX, int stepY) in Passes)
        {
            int columns = width > startX ? ((width - startX - 1) / stepX) + 1 : 0;
            int passRows = height > startY ? ((height - startY - 1) / stepY) + 1 : 0;
            if (columns == 0 || passRows == 0)
                continue;

            int rowBytes = ((columns * bitsPerPixel) + 7) / 8;
            byte[][] pass = new byte[passRows][];
            for (int row = 0; row < passRows; row++)
            {
                pass[row] = new byte[rowBytes];
                for (int column = 0; column < columns; column++)
                    Copy(rows[startY + (row * stepY)], startX + (column * stepX), pass[row], column, bitsPerPixel);
            }

            byte[] passFilters = new byte[passRows];
            for (int row = 0; row < passRows; row++)
            {
                filter.MoveNext();
                passFilters[row] = filter.Current;
            }

            data.AddRange(TestPng.Filter(pass, step, passFilters));
        }

        return data.ToArray();
    }

    private static void Copy(byte[] source, int sourcePixel, byte[] target, int targetPixel, int bitsPerPixel)
    {
        if (bitsPerPixel >= 8)
        {
            int bytes = bitsPerPixel / 8;
            Array.Copy(source, sourcePixel * bytes, target, targetPixel * bytes, bytes);
            return;
        }

        int mask = (1 << bitsPerPixel) - 1;
        int sourceBit = sourcePixel * bitsPerPixel;
        int value = (source[sourceBit / 8] >> (8 - bitsPerPixel - (sourceBit % 8))) & mask;
        int targetBit = targetPixel * bitsPerPixel;
        target[targetBit / 8] |= (byte)(value << (8 - bitsPerPixel - (targetBit % 8)));
    }
}
