namespace Rustaveli.Pdf;

/// <summary>What an image generated at its final size is asked for: the box it fills and the pixels that takes.</summary>
/// <param name="Size">The box the image fills, in points.</param>
/// <param name="PixelWidth">How many pixels across the image needs to be sharp at <paramref name="Resolution"/>.</param>
/// <param name="PixelHeight">How many pixels down.</param>
/// <param name="Resolution">The resolution images are generated at, in pixels per inch.</param>
public readonly record struct ImageRequest(Extent Size, int PixelWidth, int PixelHeight, float Resolution);
