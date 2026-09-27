using CsCheck;
using Rustaveli.Pdf.Images;
using Rustaveli.Pdf.UnitTests.Images;

namespace Rustaveli.Pdf.UnitTests.PropertyBased;

/// <summary>
/// Properties of the image loader over thousands of generated inputs: damaged images fail only in the defined way,
/// and any valid PNG — every colour type, bit depth, size, filter mix and interlacing — decodes to exactly its
/// pixels and embeds with exactly its opacity.
/// </summary>
public class ImagePropertyTests
{
    private static readonly string[] Fixtures = TestImageFiles.Valid().ToArray();

    private static readonly (int ColorType, int BitDepth)[] Layouts =
    [
        (0, 1), (0, 2), (0, 4), (0, 8), (0, 16), (2, 8), (2, 16), (3, 1), (3, 2), (3, 4), (3, 8), (4, 8), (4, 16), (6, 8),
        (6, 16),
    ];

    private static readonly Gen<byte> AlphaValue = Gen.Frequency((3, Gen.Const((byte)0)), (3, Gen.Const((byte)255)), (1, Gen.Byte));

    [Fact]
    public void DamagedImagesLoadOrFailWithTheDefinedException()
    {
        Gen.Select(
                Gen.Int[0, Fixtures.Length - 1],
                Gen.Select(Gen.Int[0, int.MaxValue], Gen.Byte).Array[1, 6],
                Gen.Int[0, 20],
                Gen.Bool)
            .Sample(sample =>
            {
                (int fixture, (int Position, byte Value)[] changes, int cut, bool repair) = sample;
                byte[] data = TestImageFiles.Bytes(Fixtures[fixture]);
                foreach ((int position, byte value) in changes)
                    data[position % data.Length] = value;

                data = data.AsSpan(0, Math.Max(0, data.Length - cut)).ToArray();
                if (repair && Fixtures[fixture].EndsWith(".png", StringComparison.Ordinal))
                    data = TestPng.RepairCrcs(data);

                MalformedImageTests.LoadsOrFailsCleanly(data);
            }, iter: 3000);
    }

    [Fact]
    public void AnyValidPngDecodesToItsPixelsAndEmbedsWithItsOpacity()
    {
        Gen.Select(
                Gen.Int[0, Layouts.Length - 1],
                Gen.Int[1, 40],
                Gen.Int[1, 20],
                Gen.Bool,
                Gen.Int,
                Gen.Byte[0, 4].Array[1, 12],
                AlphaValue.Array[0, 16])
            .Sample(sample =>
            {
                (int layout, int width, int height, bool interlaced, int seed, byte[] filters, byte[] transparency) = sample;
                (int colorType, int bitDepth) = Layouts[layout];
                Check(colorType, bitDepth, width, height, interlaced, seed, filters, transparency);
            }, iter: 1500);
    }

    private static void Check(int colorType, int bitDepth, int width, int height, bool interlaced, int seed, byte[] filters,
        byte[] transparency)
    {
        PngHeader header = new PngHeader(width, height, bitDepth, (PngColorType)colorType, interlaced);
        int rowBytes = (int)header.RowBytes(width);
        byte[][] rows = Rows(new Random(seed), height, rowBytes, width * header.BitsPerPixel);
        IEnumerable<byte> filterCycle = Enumerable.Range(0, int.MaxValue).Select(index => filters[index % filters.Length]);

        byte[] data = interlaced
            ? TestAdam7.Interlace(rows, width, header.BitsPerPixel, filterCycle)
            : TestPng.Filter(rows, header.FilterStep, filterCycle.Take(height).ToArray());

        List<byte[]> chunks = [];
        byte[] alphaTable = Enumerable.Repeat((byte)255, 256).ToArray();
        int? key = null;
        if (colorType == 3)
        {
            chunks.Add(TestPng.Chunk("PLTE", new byte[(1 << bitDepth) * 3]));
            if (transparency.Length > 0)
            {
                byte[] alpha = transparency.Take(1 << bitDepth).ToArray();
                chunks.Add(TestPng.Chunk("tRNS", alpha));
                alpha.CopyTo(alphaTable, 0);
            }
        }
        else if (colorType == 0 && transparency.Length > 0)
        {
            key = transparency[0] & ((1 << bitDepth) - 1);
            chunks.Add(TestPng.Chunk("tRNS", TestPng.BigEndian16(key.Value)));
        }

        byte[] png = TestPng.Image(width, height, bitDepth, colorType, data, interlaced, chunks.ToArray());

        Assert.Equal(rows, RowCollector.Decode(PngParser.Parse(png)).Rows);

        EncodedImage image = RasterImage.FromBytes(png).Encode();
        int sampleBytes = Math.Max(1, bitDepth / 8);
        int colorSamples = header.ColorChannels;
        byte[][] color = header.HasAlphaChannel ? SplitColor(rows, width, colorSamples * sampleBytes, sampleBytes) : rows;
        Assert.Equal(color, TestZlib.Unpredict(TestZlib.Decompress(image.Data), colorSamples, bitDepth, width));

        byte[][]? mask = image.SoftMask == null
            ? null
            : TestZlib.Unpredict(TestZlib.Decompress(image.SoftMask.Data), 1, image.SoftMask.BitsPerComponent, width);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int expected = ExpectedAlpha(rows[y], x, header, alphaTable, key);
                Assert.Equal(expected, EmbeddedAlpha(image, mask, y, rows[y], x, header));
            }
        }
    }

    // Random rows whose padding bits, past the last pixel of each row, are zero as an encoder writes them.
    private static byte[][] Rows(Random random, int height, int rowBytes, int bitsUsed)
    {
        byte[][] rows = new byte[height][];
        for (int y = 0; y < height; y++)
        {
            rows[y] = new byte[rowBytes];
            random.NextBytes(rows[y]);
            int padding = (rowBytes * 8) - bitsUsed;
            rows[y][rowBytes - 1] &= (byte)(0xFF << padding);
        }

        return rows;
    }

    private static byte[][] SplitColor(byte[][] rows, int width, int colorBytes, int alphaBytes) =>
        rows.Select(row => Enumerable.Range(0, width)
            .SelectMany(pixel => row.Skip(pixel * (colorBytes + alphaBytes)).Take(colorBytes)).ToArray()).ToArray();

    // The first byte of sample `index` of pixel `x`, or the whole sample below 8 bits.
    private static int Sample(byte[] row, int x, PngHeader header, int index)
    {
        if (header.BitDepth < 8)
        {
            int bit = x * header.BitDepth;
            return (row[bit / 8] >> (8 - header.BitDepth - (bit % 8))) & ((1 << header.BitDepth) - 1);
        }

        int sampleBytes = header.BitDepth / 8;
        int offset = ((x * header.Channels) + index) * sampleBytes;
        return sampleBytes == 1 ? row[offset] : (row[offset] << 8) | row[offset + 1];
    }

    // Opacity on a 0 to 65535 scale, so that 8-bit and 16-bit values compare exactly.
    private static int ExpectedAlpha(byte[] row, int x, PngHeader header, byte[] alphaTable, int? key) =>
        header.ColorType switch
        {
            PngColorType.Palette => alphaTable[Sample(row, x, header, 0)] * 257,
            PngColorType.GrayAlpha or PngColorType.Rgba =>
                header.BitDepth == 8 ? Sample(row, x, header, header.Channels - 1) * 257 : Sample(row, x, header, header.Channels - 1),
            _ => key == Sample(row, x, header, 0) ? 0 : 65535,
        };

    // The opacity a PDF reader would give pixel x of row y: from the soft mask, the colour key, or opaque.
    private static int EmbeddedAlpha(EncodedImage image, byte[][]? mask, int y, byte[] row, int x, PngHeader header)
    {
        if (image.SoftMask != null)
        {
            return image.SoftMask.BitsPerComponent == 8
                ? mask![y][x] * 257
                : (mask![y][x * 2] << 8) | mask[y][(x * 2) + 1];
        }

        if (image.ColorKeyMask != null)
        {
            for (int index = 0; index < header.ColorChannels; index++)
            {
                int sample = Sample(row, x, header, index);
                if (sample < image.ColorKeyMask[index * 2] || sample > image.ColorKeyMask[(index * 2) + 1])
                    return 65535;
            }

            return 0;
        }

        return 65535;
    }
}
