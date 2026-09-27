namespace Rustaveli.Pdf.Images;

/// <summary>
/// Colour and orientation information carried beside an image's pixels. Only the ICC profile affects how the
/// image is embedded; the rest is reported for the layout and for future colour management.
/// </summary>
internal sealed class ImageMetadata(
    ExifOrientation orientation = ExifOrientation.Normal,
    IccProfile? iccProfile = null,
    double? gamma = null,
    Chromaticities? chromaticities = null,
    RenderingIntent? renderingIntent = null)
{
    /// <summary>From EXIF: JPEG APP1, or a PNG eXIf chunk.</summary>
    public ExifOrientation Orientation { get; } = orientation;

    /// <summary>From JPEG APP2 segments or a PNG iCCP chunk, when valid for the image.</summary>
    public IccProfile? IccProfile { get; } = iccProfile;

    /// <summary>From a PNG gAMA chunk: the exponent that encoded the samples, e.g. 0.45455.</summary>
    public double? Gamma { get; } = gamma;

    /// <summary>From a PNG cHRM chunk.</summary>
    public Chromaticities? Chromaticities { get; } = chromaticities;

    /// <summary>From a PNG sRGB chunk, whose presence also says the samples are sRGB.</summary>
    public RenderingIntent? RenderingIntent { get; } = renderingIntent;
}
