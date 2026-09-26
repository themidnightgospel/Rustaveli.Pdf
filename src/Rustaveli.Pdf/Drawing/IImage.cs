namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// A decoded raster image ready to be placed into a document.
/// </summary>
public interface IImage
{
    /// <summary>Intrinsic width in pixels.</summary>
    int PixelWidth { get; }

    /// <summary>Intrinsic height in pixels.</summary>
    int PixelHeight { get; }

    /// <summary>Width divided by height. Used to derive layout size from one known dimension.</summary>
    float AspectRatio => (PixelHeight == 0) ? 1f : ((float)PixelWidth / (float)PixelHeight);
}
