using Rustaveli.Pdf.Images;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// The images one document shows, each written once as an image XObject however often it appears.
/// </summary>
/// <remarks>
/// Images are matched by content, not by instance, so the same logo loaded twice is still embedded once; a hash
/// finds candidates and a byte comparison confirms them. ICC profiles are shared the same way.
/// </remarks>
internal sealed class ImageEmbedder(PdfFileWriter file)
{
    private static readonly PdfName Image = new PdfName("Image");
    private static readonly PdfName Width = new PdfName("Width");
    private static readonly PdfName Height = new PdfName("Height");
    private static readonly PdfName BitsPerComponent = new PdfName("BitsPerComponent");
    private static readonly PdfName DctDecode = new PdfName("DCTDecode");
    private static readonly PdfName ColorTransform = new PdfName("ColorTransform");
    private static readonly PdfName Colors = new PdfName("Colors");
    private static readonly PdfName Decode = new PdfName("Decode");
    private static readonly PdfName Mask = new PdfName("Mask");
    private static readonly PdfName SMask = new PdfName("SMask");
    private static readonly PdfName DeviceGray = new PdfName("DeviceGray");
    private static readonly PdfName DeviceRgb = new PdfName("DeviceRGB");
    private static readonly PdfName DeviceCmyk = new PdfName("DeviceCMYK");
    private static readonly PdfName Indexed = new PdfName("Indexed");
    private static readonly PdfName IccBased = new PdfName("ICCBased");
    private static readonly PdfName Alternate = new PdfName("Alternate");

    private readonly Dictionary<ImageContentHash, List<(RasterImage Image, PdfReference Reference)>> _images = [];
    private readonly Dictionary<ImageContentHash, List<(IccProfile Profile, PdfReference Reference)>> _profiles = [];

    /// <summary>The XObject showing <paramref name="image"/>, written on its first use.</summary>
    public PdfReference Reference(RasterImage image)
    {
        if (!_images.TryGetValue(image.ContentHash, out List<(RasterImage Image, PdfReference Reference)>? written))
        {
            written = [];
            _images.Add(image.ContentHash, written);
        }

        foreach ((RasterImage candidate, PdfReference reference) in written)
        {
            if (ReferenceEquals(candidate, image) || candidate.HasSameContent(image))
                return reference;
        }

        PdfReference result = Write(image.Encode());
        written.Add((image, result));
        return result;
    }

    private PdfReference Write(EncodedImage image)
    {
        PdfDictionary dictionary = new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.XObject,
            [PdfNames.Subtype] = Image,
            [Width] = image.Width,
            [Height] = image.Height,
            [PdfNames.ColorSpace] = ColorSpace(image.ColorSpace),
            [BitsPerComponent] = image.BitsPerComponent,
            [PdfNames.Filter] = image.Filter == ImageFilter.Dct ? DctDecode : PdfNames.FlateDecode,
        };

        if (image.DecodeParameters is FlateDecodeParameters flate)
        {
            dictionary[PdfNames.DecodeParms] = new PdfDictionary
            {
                [PdfNames.Predictor] = flate.Predictor,
                [Colors] = flate.Colors,
                [BitsPerComponent] = flate.BitsPerComponent,
                [PdfNames.Columns] = flate.Columns,
            };
        }
        else if (image.ColorTransform is int transform)
        {
            // A DCTDecode parameter: whether the decoder converts YCbCr back to RGB.
            dictionary[PdfNames.DecodeParms] = new PdfDictionary { [ColorTransform] = transform };
        }

        if (image.Decode is { } decode)
            dictionary[Decode] = Numbers(decode);

        if (image.ColorKeyMask is { } key)
            dictionary[Mask] = Numbers(key.Select(value => (double)value));

        if (image.SoftMask is EncodedImage softMask)
            dictionary[SMask] = Write(softMask);

        // Already encoded — DCT or Flate with a predictor — so written as it is.
        return file.WriteStream(dictionary, image.Data.Span, PdfStreamCompression.None);
    }

    private PdfValue ColorSpace(ImageColorSpace space) => space.Kind switch
    {
        ImageColorSpaceKind.DeviceGray => DeviceGray,
        ImageColorSpaceKind.DeviceRgb => DeviceRgb,
        ImageColorSpaceKind.DeviceCmyk => DeviceCmyk,
        ImageColorSpaceKind.Indexed => new PdfArray(4)
        {
            Indexed,
            ColorSpace(space.Base!),
            space.HighValue,
            new PdfString(space.Palette.Span, PdfStringForm.Hex),
        },
        _ => new PdfArray(2) { IccBased, Profile(space.Profile!) },
    };

    private PdfReference Profile(IccProfile profile)
    {
        if (!_profiles.TryGetValue(profile.Hash, out List<(IccProfile Profile, PdfReference Reference)>? written))
        {
            written = [];
            _profiles.Add(profile.Hash, written);
        }

        foreach ((IccProfile candidate, PdfReference reference) in written)
        {
            if (candidate.Data.Span.SequenceEqual(profile.Data.Span))
                return reference;
        }

        PdfReference result = file.WriteStream(
            new PdfDictionary
            {
                [PdfNames.N] = profile.ComponentCount,
                [Alternate] = profile.ComponentCount switch { 1 => DeviceGray, 4 => DeviceCmyk, _ => DeviceRgb },
            },
            profile.Data.Span);

        written.Add((profile, result));
        return result;
    }

    private static PdfArray Numbers(IEnumerable<double> values)
    {
        PdfArray array = new PdfArray();
        foreach (double value in values)
            array.Add(value);

        return array;
    }
}
