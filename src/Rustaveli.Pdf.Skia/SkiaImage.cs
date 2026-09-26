using SkiaSharp;

namespace Rustaveli.Pdf;

/// <summary>
/// A raster image decoded by Skia.
/// </summary>
public sealed class SkiaImage : IImage, IDisposable
{
    private SkiaImage(SKImage image) => Image = image;

    internal SKImage Image { get; }

    public int PixelWidth => Image.Width;

    public int PixelHeight => Image.Height;

    public static SkiaImage FromBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        SKImage image = SKImage.FromEncodedData(data)
            ?? throw new InvalidOperationException("The data could not be decoded as an image. Supported formats are JPEG, PNG, BMP, GIF and WEBP.");

        return new SkiaImage(image);
    }

    public static SkiaImage FromFile(string path) => FromBytes(File.ReadAllBytes(path));

    public static SkiaImage FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using MemoryStream buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return FromBytes(buffer.ToArray());
    }

    public void Dispose() => Image.Dispose();
}
