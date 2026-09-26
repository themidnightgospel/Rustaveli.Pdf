using System.IO.Compression;
using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.IntegrationTests.Images;

/// <summary>
/// Reads an <see cref="EncodedImage"/> back to 8-bit RGBA the way a PDF reader would: inflate, undo the PNG
/// predictor, look palette indices up, scale samples to 8 bits, and apply the soft mask or colour key. Written
/// plainly and independently of the code under test.
/// </summary>
internal static class EmbeddedPixels
{
    public static byte[] Rgba(EncodedImage image)
    {
        byte[][] color = Rows(image);
        byte[][]? mask = image.SoftMask == null ? null : Rows(image.SoftMask);
        int components = image.ColorSpace.ComponentCount;
        byte[] rgba = new byte[image.Width * image.Height * 4];

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int[] samples = Enumerable.Range(0, components)
                    .Select(index => Sample(color[y], (x * components) + index, image.BitsPerComponent))
                    .ToArray();

                int offset = ((y * image.Width) + x) * 4;
                if (image.ColorSpace.Kind == ImageColorSpaceKind.Indexed)
                {
                    ReadOnlySpan<byte> palette = image.ColorSpace.Palette.Span;
                    rgba[offset] = palette[samples[0] * 3];
                    rgba[offset + 1] = palette[(samples[0] * 3) + 1];
                    rgba[offset + 2] = palette[(samples[0] * 3) + 2];
                }
                else
                {
                    rgba[offset] = To8(samples[0], image.BitsPerComponent);
                    rgba[offset + 1] = To8(samples[components == 1 ? 0 : 1], image.BitsPerComponent);
                    rgba[offset + 2] = To8(samples[components == 1 ? 0 : 2], image.BitsPerComponent);
                }

                rgba[offset + 3] = Alpha(image, mask, y, x, samples);
            }
        }

        return rgba;
    }

    private static byte Alpha(EncodedImage image, byte[][]? mask, int y, int x, int[] samples)
    {
        if (image.SoftMask != null)
            return To8(Sample(mask![y], x, image.SoftMask.BitsPerComponent), image.SoftMask.BitsPerComponent);

        if (image.ColorKeyMask != null)
        {
            for (int index = 0; index < samples.Length; index++)
            {
                if (samples[index] < image.ColorKeyMask[index * 2] || samples[index] > image.ColorKeyMask[(index * 2) + 1])
                    return 255;
            }

            return 0;
        }

        return 255;
    }

    private static int Sample(byte[] row, int index, int bits)
    {
        if (bits == 16)
            return (row[index * 2] << 8) | row[(index * 2) + 1];

        int bit = index * bits;
        return (row[bit / 8] >> (8 - bits - (bit % 8))) & ((1 << bits) - 1);
    }

    private static byte To8(int value, int bits) => bits switch
    {
        16 => (byte)(value >> 8),
        8 => (byte)value,
        _ => (byte)(value * 255 / ((1 << bits) - 1)),
    };

    private static byte[][] Rows(EncodedImage image)
    {
        Assert.Equal(ImageFilter.Flate, image.Filter);
        FlateDecodeParameters parameters = image.DecodeParameters!;
        Assert.Equal(15, parameters.Predictor);

        byte[] zlib = image.Data.ToArray();
        using MemoryStream input = new MemoryStream(zlib, 2, zlib.Length - 6);
        using DeflateStream inflate = new DeflateStream(input, CompressionMode.Decompress);
        using MemoryStream output = new MemoryStream();
        inflate.CopyTo(output);
        byte[] data = output.ToArray();

        int rowBytes = ((parameters.Columns * parameters.Colors * parameters.BitsPerComponent) + 7) / 8;
        int step = Math.Max(1, parameters.Colors * parameters.BitsPerComponent / 8);
        byte[][] rows = new byte[image.Height][];
        byte[] previous = new byte[rowBytes];
        for (int y = 0; y < image.Height; y++)
        {
            int start = y * (rowBytes + 1);
            byte[] row = new byte[rowBytes];
            for (int x = 0; x < rowBytes; x++)
            {
                int left = x >= step ? row[x - step] : 0;
                int above = previous[x];
                int upperLeft = x >= step ? previous[x - step] : 0;
                int prediction = data[start] switch
                {
                    0 => 0,
                    1 => left,
                    2 => above,
                    3 => (left + above) / 2,
                    _ => Paeth(left, above, upperLeft),
                };
                row[x] = (byte)(data[start + 1 + x] + prediction);
            }

            rows[y] = row;
            previous = row;
        }

        return rows;
    }

    private static int Paeth(int left, int above, int upperLeft)
    {
        int estimate = left + above - upperLeft;
        int a = Math.Abs(estimate - left);
        int b = Math.Abs(estimate - above);
        int c = Math.Abs(estimate - upperLeft);
        return a <= b && a <= c ? left : b <= c ? above : upperLeft;
    }
}
