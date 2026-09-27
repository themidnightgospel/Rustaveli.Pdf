namespace Rustaveli.Pdf.Images;

/// <summary>
/// Hard limits on what the loader accepts, so that a few forged header bytes cannot demand gigabytes.
/// </summary>
internal static class ImageLimits
{
    /// <summary>
    /// 2^28 pixels, e.g. 16384 × 16384: several times any image sensibly placed on a page. Decoding streams row by
    /// row, so this bounds work rather than memory; the one whole-image buffer, for de-interlacing, is checked
    /// separately against the largest array .NET can allocate.
    /// </summary>
    public const long MaxPixels = 1L << 28;

    /// <summary>The largest byte array every supported runtime can allocate (Array.MaxLength on .NET 6+).</summary>
    public const int MaxArrayLength = 0x7FFFFFC7;

    /// <summary>1 GiB of encoded input, read from a stream or file.</summary>
    public const long MaxSourceBytes = 1L << 30;

    /// <summary>Throws unless an image of <paramref name="width"/> × <paramref name="height"/> is allowed.</summary>
    public static void CheckDimensions(long width, long height, string format)
    {
        if (width <= 0 || height <= 0)
            throw new ImageFormatException($"The {format} image has no pixels ({width} × {height}).");

        if (width * height > MaxPixels)
        {
            throw new ImageFormatException(
                $"The {format} image is {width} × {height} pixels, more than the {MaxPixels} pixels supported.");
        }
    }
}
