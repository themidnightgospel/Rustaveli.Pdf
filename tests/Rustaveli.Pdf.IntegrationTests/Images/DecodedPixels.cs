using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.IntegrationTests.Images;

/// <summary>
/// Runs a PNG through the decoder — whether or not embedding it would need to — and expands the unfiltered rows to
/// 8-bit RGBA, applying the palette and tRNS the way PNG defines them.
/// </summary>
internal sealed class DecodedPixels : IPngRowSink
{
    private readonly List<byte[]> _rows = new List<byte[]>();

    public static byte[] Rgba(PngFile png)
    {
        DecodedPixels sink = new DecodedPixels();
        PngScanlineDecoder.Decode(png, sink);

        PngHeader header = png.Header;
        ReadOnlySpan<byte> palette = png.Palette.Span;
        ReadOnlySpan<byte> paletteAlpha = png.PaletteAlpha.Span;
        byte[] rgba = new byte[header.Width * header.Height * 4];

        for (int y = 0; y < header.Height; y++)
        {
            for (int x = 0; x < header.Width; x++)
            {
                int[] samples = Enumerable.Range(0, header.Channels)
                    .Select(index => Sample(sink._rows[y], (x * header.Channels) + index, header.BitDepth))
                    .ToArray();
                int offset = ((y * header.Width) + x) * 4;

                switch (header.ColorType)
                {
                    case PngColorType.Palette:
                        rgba[offset] = palette[samples[0] * 3];
                        rgba[offset + 1] = palette[(samples[0] * 3) + 1];
                        rgba[offset + 2] = palette[(samples[0] * 3) + 2];
                        rgba[offset + 3] = samples[0] < paletteAlpha.Length ? paletteAlpha[samples[0]] : (byte)255;
                        break;

                    case PngColorType.Gray:
                    case PngColorType.GrayAlpha:
                        rgba[offset] = rgba[offset + 1] = rgba[offset + 2] = To8(samples[0], header.BitDepth);
                        rgba[offset + 3] = header.HasAlphaChannel ? To8(samples[1], header.BitDepth) : KeyAlpha(png, samples);
                        break;

                    default:
                        rgba[offset] = To8(samples[0], header.BitDepth);
                        rgba[offset + 1] = To8(samples[1], header.BitDepth);
                        rgba[offset + 2] = To8(samples[2], header.BitDepth);
                        rgba[offset + 3] = header.HasAlphaChannel ? To8(samples[3], header.BitDepth) : KeyAlpha(png, samples);
                        break;
                }
            }
        }

        return rgba;
    }

    public void Accept(ReadOnlySpan<byte> row) => _rows.Add(row.ToArray());

    private static byte KeyAlpha(PngFile png, int[] samples) =>
        png.TransparentColor != null && png.TransparentColor.SequenceEqual(samples) ? (byte)0 : (byte)255;

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
}
