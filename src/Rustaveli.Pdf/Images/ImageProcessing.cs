namespace Rustaveli.Pdf;

/// <summary>What an <see cref="IImageProcessor"/> is asked to make of an image.</summary>
/// <param name="Source">The image as it was loaded: a PNG or JPEG, possibly turned by its EXIF orientation.</param>
/// <param name="PixelWidth">How many pixels across the image should be, the right way up.</param>
/// <param name="PixelHeight">How many pixels down.</param>
/// <param name="Quality">
/// The quality, from 1 to 100, to compress at, or null to keep the image lossless. An image with transparency is kept
/// lossless whatever the quality.
/// </param>
public readonly record struct ImageProcessing(ReadOnlyMemory<byte> Source, int PixelWidth, int PixelHeight, int? Quality);
