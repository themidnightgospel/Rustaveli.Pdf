namespace Rustaveli.Pdf.Images;

/// <summary>
/// Thrown when an image is well-formed as far as it was read, but uses a format or a feature of a format that the
/// core cannot embed without a pixel decoder.
/// </summary>
/// <remarks>
/// Derives from <see cref="ImageFormatException"/> so that a caller handling bad input needs one catch clause, while
/// a caller that can fall back to another decoder can tell the two cases apart and read <see cref="Format"/>.
/// </remarks>
internal sealed class UnsupportedImageFormatException : ImageFormatException
{
    public UnsupportedImageFormatException(ImageFormat format, string message)
        : base(message)
    {
        Format = format;
    }

    /// <summary>The format that was recognised.</summary>
    public ImageFormat Format { get; }
}
