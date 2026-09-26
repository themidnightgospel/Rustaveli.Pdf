using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.Images;

/// <summary>
/// An encoded JPEG or PNG image, loaded and validated but not decoded: its size, orientation and colour metadata
/// are known at once, and its PDF encoding is produced the first time it is asked for.
/// </summary>
/// <remarks>
/// <para>
/// Loading reads headers and chunk structure only, so the layout can measure an image without paying for its
/// pixels. <see cref="Encode"/> does the work — trivial for a JPEG or a PNG that passes straight through, a full
/// decode otherwise — once, on first use, and caches the result; it is safe to call from several threads.
/// </para>
/// <para>
/// An image placed many times should be embedded once. <see cref="ContentHash"/> identifies candidates cheaply, and
/// <see cref="HasSameContent"/> confirms them, so a writer can key its image objects on content rather than on
/// which <see cref="RasterImage"/> instance a caller happened to load.
/// </para>
/// </remarks>
internal sealed class RasterImage : IImage
{
    private readonly byte[] _source;
    private readonly Lazy<EncodedImage> _encoded;

    private RasterImage(
        byte[] source,
        ImageFormat format,
        int width,
        int height,
        ImageMetadata metadata,
        Func<EncodedImage> encode)
    {
        _source = source;
        Format = format;
        PixelWidth = width;
        PixelHeight = height;
        Metadata = metadata;
        ContentHash = ImageContentHash.Of(source);
        _encoded = new Lazy<EncodedImage>(encode, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ImageFormat Format { get; }

    public int PixelWidth { get; }

    public int PixelHeight { get; }

    /// <summary>Orientation, ICC profile and colour information found beside the pixels.</summary>
    public ImageMetadata Metadata { get; }

    /// <summary>The EXIF orientation the layout applies when placing the image.</summary>
    public ExifOrientation Orientation => Metadata.Orientation;

    /// <summary>The bytes the image was loaded from, unchanged.</summary>
    public ReadOnlyMemory<byte> Source => _source;

    /// <summary>A fingerprint of <see cref="Source"/>.</summary>
    public ImageContentHash ContentHash { get; }

    /// <summary>
    /// Loads an image from <paramref name="data"/>. The array is copied, so the caller may reuse it afterwards.
    /// </summary>
    /// <exception cref="ImageFormatException">The data is not a well-formed JPEG or PNG within the limits.</exception>
    /// <exception cref="UnsupportedImageFormatException">The data is a format, or uses a feature, that cannot be embedded.</exception>
    public static RasterImage FromBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Load((byte[])data.Clone());
    }

    /// <summary>
    /// Loads an image from the rest of <paramref name="stream"/>, which need not be seekable. The stream is read to
    /// its end but not disposed.
    /// </summary>
    public static RasterImage FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Load(ImageSourceReader.ReadAll(stream));
    }

    /// <summary>Loads an image from the file at <paramref name="path"/>.</summary>
    public static RasterImage FromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Load(ImageSourceReader.ReadAll(file));
    }

    /// <summary>Loads an image from an array the caller hands over and will not modify.</summary>
    internal static RasterImage Load(byte[] data)
    {
        ImageFormat format = ImageFormatDetector.Detect(data);
        switch (format)
        {
            case ImageFormat.Jpeg:
                JpegFile jpeg = JpegParser.Parse(data);
                return new RasterImage(
                    data,
                    format,
                    jpeg.Width,
                    jpeg.Height,
                    new ImageMetadata(jpeg.Orientation, jpeg.IccProfile),
                    jpeg.Encode);

            case ImageFormat.Png:
                PngFile png = PngParser.Parse(data);
                return new RasterImage(
                    data,
                    format,
                    png.Header.Width,
                    png.Header.Height,
                    png.Metadata,
                    () => PngImageEncoder.Encode(png));

            case ImageFormat.Unknown:
                throw new ImageFormatException("The data is not in a recognised image format.");

            default:
                throw new UnsupportedImageFormatException(
                    format,
                    $"{format} images cannot be embedded yet; convert the image to PNG or JPEG.");
        }
    }

    /// <summary>
    /// The image as a PDF image XObject stores it. Computed on first call — for most PNGs with transparency or
    /// interlacing, that is when the pixels are decoded — and cached.
    /// </summary>
    /// <exception cref="ImageFormatException">The compressed image data turned out to be corrupt.</exception>
    public EncodedImage Encode() => _encoded.Value;

    /// <summary>
    /// True if <paramref name="other"/> was loaded from exactly the same bytes. Meant to confirm a match found by
    /// <see cref="ContentHash"/>: the comparison stops at the first difference, and at once for different lengths.
    /// </summary>
    public bool HasSameContent(RasterImage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return _source.AsSpan().SequenceEqual(other._source);
    }
}
