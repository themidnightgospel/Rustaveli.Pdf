using SkiaSharp;

namespace Rustaveli.Pdf.Raster;

/// <summary>Pages drawn onto pixels at a resolution and encoded as images.</summary>
internal sealed class RasterPageTarget(ImageExportOptions options) : ISkiaPageTarget
{
    private SKSurface? _surface;

    public SKCanvas Begin(Extent size, out SKPoint unitsPerPoint)
    {
        if (_surface is not null)
            throw new InvalidOperationException("A page is already open. EndPage must be called before the next BeginPage.");

        float scale = options.Resolution / 72f;
        SKImageInfo info = new SKImageInfo(Pixels(size.Width, scale), Pixels(size.Height, scale), SKColorType.Rgba8888, SKAlphaType.Premul);

        _surface = SKSurface.Create(info) ?? throw new InvalidOperationException($"Skia could not allocate a {info.Width}×{info.Height} page.");
        _surface.Canvas.Clear(options.Format == PageImageFormat.Jpeg ? SKColors.White : SKColors.Transparent);

        // The page fills the whole pixel grid, as a viewer rendering it at this resolution fills it: scaling by the
        // resolution alone would leave the rounding of the size to drift across the page.
        unitsPerPoint = new SKPoint(info.Width / size.Width, info.Height / size.Height);
        return _surface.Canvas;
    }

    public byte[] End()
    {
        SKSurface surface = _surface ?? throw new InvalidOperationException("No page is open. BeginPage must be called before drawing.");
        _surface = null;

        using (surface)
        {
            using SKImage snapshot = surface.Snapshot();
            using SKData data = snapshot.Encode(Encoding(options.Format), options.Quality);
            return data.ToArray();
        }
    }

    public bool TextAsOutlines => false;

    public void Dispose()
    {
        _surface?.Dispose();
        _surface = null;
    }

    /// <summary>
    /// The pixels <paramref name="points"/> take at <paramref name="scale"/>, at least one. A count beyond what an int
    /// holds is refused here: converted, it would come out as whatever the platform makes of it — on .NET Framework the
    /// smallest int, which would have drawn a page of a single pixel.
    /// </summary>
    private static int Pixels(float points, float scale)
    {
        double pixels = Math.Round((double)points * scale, MidpointRounding.AwayFromZero);

        if (pixels > int.MaxValue)
            throw new InvalidOperationException(FormattableString.Invariant($"A page {points} points long at {scale * 72} pixels per inch has more pixels than an image can have."));

        return Math.Max(1, (int)pixels);
    }

    private static SKEncodedImageFormat Encoding(PageImageFormat format) => format switch
    {
        PageImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
        PageImageFormat.Webp => SKEncodedImageFormat.Webp,
        _ => SKEncodedImageFormat.Png,
    };
}
