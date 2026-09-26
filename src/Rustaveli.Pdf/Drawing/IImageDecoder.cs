namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// Decodes image bytes into a backend-specific <see cref="IImage"/>.
/// </summary>
internal interface IImageDecoder
{
    IImage Decode(byte[] data);
}
