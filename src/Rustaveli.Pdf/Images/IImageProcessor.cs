namespace Rustaveli.Pdf;

/// <summary>
/// Re-encodes images for embedding: scales them to the pixels they are shown at and compresses them at a quality.
/// The core embeds images as they are; a processor, such as <c>SkiaImageProcessor</c> in the Raster package, is
/// only needed when an image or the export asks for a quality or a maximum resolution.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// The image <paramref name="request"/> describes, the right way up, re-encoded as a PNG or JPEG of the pixels asked
    /// for.
    /// </summary>
    byte[] Process(ImageProcessing request);
}
