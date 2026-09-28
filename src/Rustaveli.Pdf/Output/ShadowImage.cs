using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// Writes a blurred shadow as an image: a single pixel of the shadow's ink, stretched over the mask's grid and seen
/// through the mask as its soft mask, which a viewer smooths as it scales.
/// </summary>
internal static class ShadowImage
{
    private static readonly PdfName Image = new PdfName("Image");
    private static readonly PdfName Width = new PdfName("Width");
    private static readonly PdfName Height = new PdfName("Height");
    private static readonly PdfName BitsPerComponent = new PdfName("BitsPerComponent");
    private static readonly PdfName SMask = new PdfName("SMask");
    private static readonly PdfName Interpolate = new PdfName("Interpolate");
    private static readonly PdfName DeviceGray = new PdfName("DeviceGray");
    private static readonly PdfName DeviceRgb = new PdfName("DeviceRGB");
    private static readonly PdfName DeviceCmyk = new PdfName("DeviceCMYK");

    /// <summary>The image XObject for a shadow of <paramref name="ink"/> covering <paramref name="mask"/>.</summary>
    /// <param name="file">Where the image is written.</param>
    /// <param name="mask">How much of the shadow covers each point.</param>
    /// <param name="ink">The shadow's colour.</param>
    /// <param name="archival">
    /// Whether the file is PDF/A, which has the ink written in RGB and forbids asking a viewer to smooth the mask; the
    /// mask, sampled finely for its blur, is shown as it is.
    /// </param>
    public static PdfReference Write(PdfFileWriter file, ShadowMask mask, Ink ink, bool archival = false)
    {
        PdfReference softMask = file.WriteStream(
            Dictionary(mask.Width, mask.Height, DeviceGray, interpolate: !archival),
            mask.Coverage);

        // A soft mask need not match its image's size, so the ink is one pixel however large the shadow.
        bool cmyk = !archival && ink.Model == InkModel.Cmyk;
        PdfDictionary image = Dictionary(1, 1, cmyk ? DeviceCmyk : DeviceRgb, interpolate: !archival);
        image[SMask] = softMask;

        return file.WriteStream(image, cmyk ? Cmyk(ink) : Rgb(ink));
    }

    private static PdfDictionary Dictionary(int width, int height, PdfName space, bool interpolate)
    {
        PdfDictionary dictionary = new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.XObject,
            [PdfNames.Subtype] = Image,
            [Width] = width,
            [Height] = height,
            [PdfNames.ColorSpace] = space,
            [BitsPerComponent] = 8,
        };

        if (interpolate)
            dictionary[Interpolate] = true;

        return dictionary;
    }

    private static byte[] Rgb(Ink ink)
    {
        (float red, float green, float blue) = ink.ToRgb();
        return [ToByte(red), ToByte(green), ToByte(blue)];
    }

    private static byte[] Cmyk(Ink ink)
    {
        (float cyan, float magenta, float yellow, float black) = ink.ToCmyk();
        return [ToByte(cyan), ToByte(magenta), ToByte(yellow), ToByte(black)];
    }

    private static byte ToByte(float fraction) => (byte)Math.Round(Math.Min(1f, Math.Max(0f, fraction)) * 255);
}
