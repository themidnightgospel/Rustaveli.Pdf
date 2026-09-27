namespace Rustaveli.Pdf.Images;

/// <summary>
/// An image in the form a PDF image XObject stores it: everything needed to write the stream dictionary, and the
/// stream's bytes, already encoded. Nothing here needs further processing — each member maps onto one dictionary
/// entry.
/// </summary>
internal sealed class EncodedImage
{
    public EncodedImage(
        int width,
        int height,
        ImageColorSpace colorSpace,
        int bitsPerComponent,
        ImageFilter filter,
        ReadOnlyMemory<byte> data,
        FlateDecodeParameters? decodeParameters = null,
        int? colorTransform = null,
        IReadOnlyList<double>? decode = null,
        IReadOnlyList<int>? colorKeyMask = null,
        EncodedImage? softMask = null)
    {
        Width = width;
        Height = height;
        ColorSpace = colorSpace;
        BitsPerComponent = bitsPerComponent;
        Filter = filter;
        Data = data;
        DecodeParameters = decodeParameters;
        ColorTransform = colorTransform;
        Decode = decode;
        ColorKeyMask = colorKeyMask;
        SoftMask = softMask;
    }

    /// <summary>/Width, in pixels.</summary>
    public int Width { get; }

    /// <summary>/Height, in pixels.</summary>
    public int Height { get; }

    /// <summary>/ColorSpace.</summary>
    public ImageColorSpace ColorSpace { get; }

    /// <summary>/BitsPerComponent: 1, 2, 4, 8 or 16.</summary>
    public int BitsPerComponent { get; }

    /// <summary>/Filter.</summary>
    public ImageFilter Filter { get; }

    /// <summary>The stream data, encoded with <see cref="Filter"/>.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// /DecodeParms for <see cref="ImageFilter.Flate"/> data that carries PNG row prediction; null when the data
    /// is not predicted.
    /// </summary>
    public FlateDecodeParameters? DecodeParameters { get; }

    /// <summary>
    /// /DecodeParms &lt;&lt; /ColorTransform n &gt;&gt; for <see cref="ImageFilter.Dct"/> data, when the reader's
    /// default would be wrong; null to write no parameters. Set to 0 for a three-component JPEG whose components
    /// are RGB rather than YCbCr, which nothing inside the file tells a PDF reader.
    /// </summary>
    public int? ColorTransform { get; }

    /// <summary>
    /// /Decode, or null for the default. Set to [1 0 1 0 1 0 1 0] for Adobe CMYK JPEGs, which store inverted
    /// values.
    /// </summary>
    public IReadOnlyList<double>? Decode { get; }

    /// <summary>
    /// /Mask as an array of colour-key ranges [min1 max1 … minN maxN] in sample values (palette indices for an
    /// indexed image): pixels whose every sample lies in its range are not painted. Null when there is none.
    /// </summary>
    public IReadOnlyList<int>? ColorKeyMask { get; }

    /// <summary>
    /// /SMask: a /DeviceGray image of the same width and height giving each pixel's opacity. Null when the image
    /// is opaque or masked by <see cref="ColorKeyMask"/> instead.
    /// </summary>
    public EncodedImage? SoftMask { get; }
}
