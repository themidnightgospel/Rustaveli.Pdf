namespace Rustaveli.Pdf.Images;

/// <summary>
/// Turns a parsed PNG into an <see cref="EncodedImage"/>, passing its compressed data straight through whenever
/// PDF can read it as it is.
/// </summary>
/// <remarks>
/// <para>
/// PNG's image data is a zlib stream of rows that each begin with a filter-type byte, and /FlateDecode with
/// /Predictor 15 is defined to read exactly that. So an image PDF can represent directly — not interlaced, no alpha
/// channel, and any transparency expressible as a colour key — is embedded by concatenating its IDAT payloads: no
/// inflating, no filtering, no deflating, and the output is as small as the PNG was. That includes 16-bit images,
/// which PDF 1.5 and later allow and which PDFium, pdf.js, MuPDF, Poppler and Acrobat all read.
/// </para>
/// <para>
/// Everything else is decoded and compressed again by <see cref="PngReencoder"/>: interlaced images, because PDF
/// knows no interlacing; alpha channels, because PDF keeps opacity in a separate soft mask; and palettes with
/// partial transparency, for the same reason.
/// </para>
/// <para>
/// Transparency that is all-or-nothing becomes a /Mask colour key wherever that is exact, which needs no mask image
/// at all: the single tRNS colour of a gray or RGB image, and a palette whose fully transparent entries form one
/// contiguous run of indices.
/// </para>
/// </remarks>
internal static class PngImageEncoder
{
    private enum PaletteTransparency
    {
        /// <summary>No tRNS, or every entry opaque.</summary>
        None,

        /// <summary>Every entry fully opaque or fully transparent, the transparent ones contiguous.</summary>
        ColorKey,

        /// <summary>Partial alpha, or transparent entries scattered: a soft mask is needed.</summary>
        SoftMask,
    }

    public static EncodedImage Encode(PngFile png)
    {
        PngHeader header = png.Header;
        PaletteTransparency transparency = Classify(png.PaletteAlpha.Span, out int firstTransparent, out int lastTransparent);
        IReadOnlyList<int>? colorKey = transparency == PaletteTransparency.ColorKey
            ? [firstTransparent, lastTransparent]
            : ColorKey(png.TransparentColor);

        bool paletteMask = transparency == PaletteTransparency.SoftMask;
        ImageColorSpace colorSpace = ColorSpaceOf(png);
        FlateDecodeParameters parameters = new FlateDecodeParameters(header.ColorChannels, header.BitDepth, header.Width);

        if (!header.Interlaced && !header.HasAlphaChannel && !paletteMask)
        {
            return new EncodedImage(
                header.Width,
                header.Height,
                colorSpace,
                header.BitDepth,
                ImageFilter.Flate,
                png.ConcatenateImageData(),
                parameters,
                colorKeyMask: colorKey);
        }

        using PngReencoder reencoder = new PngReencoder(header, png.PaletteAlpha.Span, paletteMask);
        PngScanlineDecoder.Decode(png, reencoder);

        byte[] color = reencoder.FinishColor();
        byte[]? alpha = reencoder.FinishAlpha();
        EncodedImage? softMask = alpha == null
            ? null
            : new EncodedImage(
                header.Width,
                header.Height,
                ImageColorSpace.DeviceGray,
                reencoder.AlphaBitDepth,
                ImageFilter.Flate,
                alpha,
                new FlateDecodeParameters(1, reencoder.AlphaBitDepth, header.Width));

        return new EncodedImage(
            header.Width,
            header.Height,
            colorSpace,
            header.BitDepth,
            ImageFilter.Flate,
            color,
            parameters,
            colorKeyMask: colorKey,
            softMask: softMask);
    }

    private static ImageColorSpace ColorSpaceOf(PngFile png)
    {
        IccProfile? profile = png.Metadata.IccProfile;
        return png.Header.ColorType switch
        {
            PngColorType.Palette => ImageColorSpace.Indexed(ImageColorSpace.For(3, profile), png.Palette),
            PngColorType.Gray or PngColorType.GrayAlpha => ImageColorSpace.For(1, profile),
            _ => ImageColorSpace.For(3, profile),
        };
    }

    // A tRNS colour matches samples exactly, and a colour-key range with equal ends does the same.
    private static IReadOnlyList<int>? ColorKey(IReadOnlyList<int>? transparentColor)
    {
        if (transparentColor == null)
            return null;

        int[] ranges = new int[transparentColor.Count * 2];
        for (int index = 0; index < transparentColor.Count; index++)
        {
            ranges[index * 2] = transparentColor[index];
            ranges[(index * 2) + 1] = transparentColor[index];
        }

        return ranges;
    }

    private static PaletteTransparency Classify(ReadOnlySpan<byte> alpha, out int first, out int last)
    {
        first = -1;
        last = -1;
        bool contiguous = true;

        for (int index = 0; index < alpha.Length; index++)
        {
            if (alpha[index] == 0)
            {
                if (first < 0)
                    first = index;
                else if (last != index - 1)
                    contiguous = false;
                last = index;
            }
            else if (alpha[index] != 0xFF)
            {
                return PaletteTransparency.SoftMask;
            }
        }

        if (first < 0)
            return PaletteTransparency.None;

        return contiguous ? PaletteTransparency.ColorKey : PaletteTransparency.SoftMask;
    }
}
