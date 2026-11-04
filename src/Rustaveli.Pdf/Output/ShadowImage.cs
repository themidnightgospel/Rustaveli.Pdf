using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// Writes a blurred shadow as an image: a single pixel of the shadow's ink, stretched over the mask's grid and seen
/// through the mask as its soft mask, which a viewer smooths as it scales.
/// </summary>
internal static class ShadowImage
{
    private static readonly PdfName Interpolate = new PdfName("Interpolate");

    /// <summary>The image XObject for a shadow of <paramref name="ink"/> covering <paramref name="mask"/>.</summary>
    /// <param name="file">Where the image is written.</param>
    /// <param name="mask">How much of the shadow covers each point.</param>
    /// <param name="ink">The shadow's colour.</param>
    /// <param name="archival">
    /// Whether the file is PDF/A, which has the ink written in RGB and forbids asking a viewer to smooth the mask; the
    /// mask, sampled finely for its blur, is shown as it is.
    /// </param>
    public static PdfReference Write(PdfFileWriter file, ShadowMask mask, Ink ink, bool archival)
    {
        PdfReference softMask = file.WriteStream(
            Dictionary(mask.Width, mask.Height, PdfNames.DeviceGray, interpolate: !archival),
            mask.Coverage);

        // A soft mask need not match its image's size, so the ink is one pixel however large the shadow.
        bool cmyk = !archival && ink.Model == InkModel.Cmyk;
        PdfDictionary image = Dictionary(1, 1, cmyk ? PdfNames.DeviceCMYK : PdfNames.DeviceRGB, interpolate: !archival);
        image[PdfNames.SMask] = softMask;

        return file.WriteStream(image, cmyk ? Cmyk(ink) : Rgb(ink));
    }

    private static PdfDictionary Dictionary(int width, int height, PdfName space, bool interpolate)
    {
        PdfDictionary dictionary = new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.XObject,
            [PdfNames.Subtype] = PdfNames.Image,
            [PdfNames.Width] = width,
            [PdfNames.Height] = height,
            [PdfNames.ColorSpace] = space,
            [PdfNames.BitsPerComponent] = 8,
        };

        if (interpolate)
            dictionary[Interpolate] = true;

        return dictionary;
    }

    private static byte[] Rgb(Ink ink)
    {
        (float red, float green, float blue) = ink.ToRgb();
        return [Clamp.ToByte(red), Clamp.ToByte(green), Clamp.ToByte(blue)];
    }

    private static byte[] Cmyk(Ink ink)
    {
        (float cyan, float magenta, float yellow, float black) = ink.ToCmyk();
        return [Clamp.ToByte(cyan), Clamp.ToByte(magenta), Clamp.ToByte(yellow), Clamp.ToByte(black)];
    }
}
