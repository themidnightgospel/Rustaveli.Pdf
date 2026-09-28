using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf;

/// <summary>
/// A JPEG or PNG image, to place with <see cref="FrameContent.Image(IFrame, IImage, ImageFitting)"/>. Loading
/// validates it without decoding it; a JPEG, and most PNGs, are embedded exactly as they were encoded.
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
public sealed class RasterImage : IImage
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
        StoredWidth = width;
        StoredHeight = height;
        Metadata = metadata;
        ContentHash = ImageContentHash.Of(source);
        _encoded = new Lazy<EncodedImage>(encode, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>The same image with other settings for how it is embedded; the pixels are shared, not copied.</summary>
    private RasterImage(RasterImage original, int? quality, float? maximumResolution)
    {
        _source = original._source;
        Format = original.Format;
        StoredWidth = original.StoredWidth;
        StoredHeight = original.StoredHeight;
        Metadata = original.Metadata;
        ContentHash = original.ContentHash;
        _encoded = original._encoded;
        Quality = quality;
        MaximumResolution = maximumResolution;
    }

    /// <summary>
    /// The quality, from 1 to 100, the image is compressed at when it is embedded, or null to keep its own
    /// encoding unless the export asks otherwise.
    /// </summary>
    public int? Quality { get; }

    /// <summary>
    /// The most pixels per inch the image is embedded at where it is shown, or null to keep all its pixels unless the
    /// export asks otherwise.
    /// </summary>
    public float? MaximumResolution { get; }

    /// <summary>
    /// The image, to be compressed at <paramref name="quality"/> from 1, smallest, to 100, finest, when embedded —
    /// as JPEG, or losslessly where it has transparency. Needs an image processor at export.
    /// </summary>
    public RasterImage WithQuality(int quality)
    {
        if (quality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Quality runs from 1 to 100.");

        return new RasterImage(this, quality, MaximumResolution);
    }

    /// <summary>
    /// The image, embedded at no more than <paramref name="pixelsPerInch"/> where it is shown: an image placed smaller
    /// than its pixels need is scaled down. Needs an image processor at export.
    /// </summary>
    public RasterImage WithMaximumResolution(float pixelsPerInch)
    {
        if (!(pixelsPerInch > 0) || float.IsInfinity(pixelsPerInch))
            throw new ArgumentOutOfRangeException(nameof(pixelsPerInch), pixelsPerInch, "A resolution is a finite number of pixels per inch above nothing.");

        return new RasterImage(this, Quality, pixelsPerInch);
    }

    /// <summary>The width in pixels, the right way up: an EXIF orientation that turns the image is applied.</summary>
    public int PixelWidth => IsTurned ? StoredHeight : StoredWidth;

    /// <summary>The height in pixels, the right way up.</summary>
    public int PixelHeight => IsTurned ? StoredWidth : StoredHeight;

    internal ImageFormat Format { get; }

    /// <summary>The width as the pixels are stored, before any EXIF orientation.</summary>
    internal int StoredWidth { get; }

    /// <summary>The height as the pixels are stored, before any EXIF orientation.</summary>
    internal int StoredHeight { get; }

    /// <summary>Orientation, ICC profile and colour information found beside the pixels.</summary>
    internal ImageMetadata Metadata { get; }

    /// <summary>The EXIF orientation the layout applies when placing the image.</summary>
    internal ExifOrientation Orientation => Metadata.Orientation;

    /// <summary>The bytes the image was loaded from, unchanged.</summary>
    internal ReadOnlyMemory<byte> Source => _source;

    /// <summary>A fingerprint of <see cref="Source"/>.</summary>
    internal ImageContentHash ContentHash { get; }

    /// <summary>True when the orientation turns the image a quarter, so its upright width is its stored height.</summary>
    private bool IsTurned => Orientation is >= ExifOrientation.Transpose and <= ExifOrientation.Rotate270;

    /// <summary>
    /// Loads an image from <paramref name="data"/>. The array is copied, so the caller may reuse it afterwards.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The data is not a well-formed JPEG or PNG, or uses a feature — such as a GIF or a lossless JPEG — that cannot
    /// be embedded. The inner exception says which.
    /// </exception>
    public static RasterImage FromBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Loading(() => Load((byte[])data.Clone()), nameof(data));
    }

    /// <summary>
    /// Loads an image from the rest of <paramref name="stream"/>, which need not be seekable. The stream is read to
    /// its end but not disposed.
    /// </summary>
    /// <exception cref="ArgumentException">The stream does not hold a JPEG or PNG that can be embedded.</exception>
    public static RasterImage FromStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Loading(() => Load(ImageSourceReader.ReadAll(stream)), nameof(stream));
    }

    /// <summary>Loads an image from the file at <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentException">The file is not a JPEG or PNG that can be embedded.</exception>
    public static RasterImage FromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Loading(() => Load(ImageSourceReader.ReadAll(file)), nameof(path));
    }

    private static RasterImage Loading(Func<RasterImage> load, string parameter)
    {
        try
        {
            return load();
        }
        catch (ImageFormatException exception)
        {
            throw new ArgumentException("This is not an image that can be embedded: " + exception.Message, parameter, exception);
        }
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
    internal EncodedImage Encode() => _encoded.Value;

    /// <summary>
    /// True if <paramref name="other"/> was loaded from exactly the same bytes. Meant to confirm a match found by
    /// <see cref="ContentHash"/>: the comparison stops at the first difference, and at once for different lengths.
    /// </summary>
    internal bool HasSameContent(RasterImage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return _source.AsSpan().SequenceEqual(other._source);
    }
}
