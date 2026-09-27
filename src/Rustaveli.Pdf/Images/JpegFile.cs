namespace Rustaveli.Pdf.Images;

/// <summary>
/// What <see cref="JpegParser"/> learned from a JPEG's marker segments, and the passthrough encoding built from it.
/// </summary>
internal sealed class JpegFile
{
    // Adobe CMYK JPEGs, as written by Photoshop and most tools after it, store every CMYK value inverted.
    private static readonly IReadOnlyList<double> InvertedCmyk = [1, 0, 1, 0, 1, 0, 1, 0];

    public JpegFile(
        ReadOnlyMemory<byte> source,
        int width,
        int height,
        int componentCount,
        bool isProgressive,
        bool hasAdobeMarker,
        bool isRgb,
        ExifOrientation orientation,
        IccProfile? iccProfile)
    {
        Source = source;
        Width = width;
        Height = height;
        ComponentCount = componentCount;
        IsProgressive = isProgressive;
        HasAdobeMarker = hasAdobeMarker;
        IsRgb = isRgb;
        Orientation = orientation;
        IccProfile = iccProfile;
    }

    /// <summary>The complete file.</summary>
    public ReadOnlyMemory<byte> Source { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>1 (gray), 3 (YCbCr or RGB) or 4 (CMYK or YCCK).</summary>
    public int ComponentCount { get; }

    /// <summary>True for a progressive (SOF2) frame, false for baseline or extended sequential.</summary>
    public bool IsProgressive { get; }

    /// <summary>True when an Adobe APP14 segment is present.</summary>
    public bool HasAdobeMarker { get; }

    /// <summary>
    /// True when three components hold RGB but no marker says so to a PDF reader: no Adobe segment, no JFIF
    /// segment (which would mandate YCbCr), and component identifiers 'R', 'G', 'B'.
    /// </summary>
    public bool IsRgb { get; }

    public ExifOrientation Orientation { get; }

    public IccProfile? IccProfile { get; }

    /// <summary>
    /// The file unchanged, as DCTDecode data. The pixels are never decoded: the PDF reader's JPEG decoder does
    /// that, which is also why the original quality is preserved exactly.
    /// </summary>
    public EncodedImage Encode()
    {
        // DCTDecode's default ColorTransform is 1 for three components, a YCbCr-to-RGB conversion that would
        // scramble the colours of an RGB-coded file. An Adobe segment overrides the default by itself.
        int? colorTransform = IsRgb ? 0 : null;
        IReadOnlyList<double>? decode = ComponentCount == 4 && HasAdobeMarker ? InvertedCmyk : null;

        return new EncodedImage(
            Width,
            Height,
            ImageColorSpace.For(ComponentCount, IccProfile),
            bitsPerComponent: 8,
            ImageFilter.Dct,
            Source,
            colorTransform: colorTransform,
            decode: decode);
    }
}
