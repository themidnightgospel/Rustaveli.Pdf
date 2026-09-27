namespace Rustaveli.Pdf.Images;

/// <summary>The fields of a PNG IHDR chunk, and the row geometry that follows from them.</summary>
internal sealed class PngHeader(int width, int height, int bitDepth, PngColorType colorType, bool interlaced)
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    /// <summary>Bits per sample: 1, 2, 4, 8 or 16 (palette indices: 1, 2, 4 or 8).</summary>
    public int BitDepth { get; } = bitDepth;

    public PngColorType ColorType { get; } = colorType;

    /// <summary>True for Adam7 interlacing.</summary>
    public bool Interlaced { get; } = interlaced;

    /// <summary>Samples per pixel, alpha included.</summary>
    public int Channels => ColorType switch
    {
        PngColorType.Rgb => 3,
        PngColorType.GrayAlpha => 2,
        PngColorType.Rgba => 4,
        _ => 1,
    };

    /// <summary>Samples per pixel that carry colour: the channels less alpha.</summary>
    public int ColorChannels => HasAlphaChannel ? Channels - 1 : Channels;

    public bool HasAlphaChannel => ColorType is PngColorType.GrayAlpha or PngColorType.Rgba;

    public int BitsPerPixel => Channels * BitDepth;

    /// <summary>
    /// The distance, in bytes, between a byte and the corresponding byte of the previous pixel as the filters see
    /// it: whole pixels at 8 bits and above, and 1 below, where several pixels share a byte.
    /// </summary>
    public int FilterStep => Math.Max(1, BitsPerPixel / 8);

    /// <summary>Bytes in an unfiltered row of <paramref name="pixels"/> pixels, less the filter-type byte.</summary>
    public long RowBytes(long pixels) => ((pixels * BitsPerPixel) + 7) / 8;
}
