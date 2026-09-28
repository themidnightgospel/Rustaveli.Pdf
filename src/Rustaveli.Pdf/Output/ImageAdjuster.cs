using Rustaveli.Pdf.Images;

namespace Rustaveli.Pdf.Output;

/// <summary>
/// Makes each image what its settings, or the export's, ask for where it is shown: no more pixels than its maximum
/// resolution needs at the size it is placed, compressed at its quality. An image asking for neither is left as it is.
/// </summary>
/// <remarks>
/// The same image shown at the same size is processed once and embedded once.
/// </remarks>
internal sealed class ImageAdjuster(PdfExportOptions? options)
{
    private readonly Dictionary<(ImageContentHash Content, int Width, int Height, int? Quality), RasterImage> _adjusted = [];

    public RasterImage Adjust(RasterImage image, Extent size)
    {
        int? quality = image.Quality ?? options?.ImageQuality;
        float? resolution = image.MaximumResolution ?? options?.MaximumImageResolution;

        if (quality is null && resolution is null)
            return image;

        int width = image.PixelWidth;
        int height = image.PixelHeight;

        if (resolution is { } pixelsPerInch)
        {
            width = Math.Min(width, Math.Max(1, (int)Math.Ceiling(size.Width / 72 * pixelsPerInch)));
            height = Math.Min(height, Math.Max(1, (int)Math.Ceiling(size.Height / 72 * pixelsPerInch)));
        }

        // Fine enough already, and not to be recompressed: nothing to do.
        if (quality is null && width == image.PixelWidth && height == image.PixelHeight)
            return image;

        (ImageContentHash, int, int, int?) key = (image.ContentHash, width, height, quality);

        if (_adjusted.TryGetValue(key, out RasterImage? adjusted))
            return adjusted;

        IImageProcessor processor = options?.ImageProcessor ?? throw new InvalidOperationException(
            "An image asks for a quality or a maximum resolution, which needs PdfExportOptions.ImageProcessor: " +
            "SkiaImageProcessor from the Rustaveli.Pdf.Raster package, for one.");

        adjusted = RasterImage.Load(processor.Process(new ImageProcessing(image.Source, width, height, quality)));
        _adjusted.Add(key, adjusted);
        return adjusted;
    }
}
