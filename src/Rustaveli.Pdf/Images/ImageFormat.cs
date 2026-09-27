namespace Rustaveli.Pdf.Images;

/// <summary>
/// The container format of an encoded image, as recognised from its leading bytes.
/// </summary>
/// <remarks>
/// Only <see cref="Jpeg"/> and <see cref="Png"/> can be embedded by the core. The others are recognised so that the
/// caller receives an <see cref="UnsupportedImageFormatException"/> naming the format rather than a generic
/// "corrupt data" error, and so that an optional decoder can later take them over.
/// </remarks>
internal enum ImageFormat
{
    Unknown,
    Jpeg,
    Png,
    Gif,
    Bmp,
    WebP,
    Tiff,
    Heic,
    Avif,
}
