namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// Decodes image bytes into a backend-specific <see cref="IImage"/>.
/// </summary>
public interface IImageDecoder
{
    IImage Decode(byte[] data);
}
