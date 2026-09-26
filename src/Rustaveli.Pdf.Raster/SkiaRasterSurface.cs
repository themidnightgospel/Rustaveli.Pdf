using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Images;
using Rustaveli.Pdf.Text;
using SkiaSharp;

namespace Rustaveli.Pdf.Raster;

/// <summary>
/// Draws pages into images with SkiaSharp, one bitmap per page, encoded as each page ends.
/// </summary>
/// <remarks>
/// Text is set from the same glyph walk the layout measured and the PDF surface shows: each face is loaded into
/// Skia from the very bytes the core parsed, and every glyph is placed at the position layout computed, so a page
/// image and a PDF page agree glyph for glyph. Images are decoded as stored and turned by their EXIF orientation
/// here, as the PDF surface does, rather than by whatever the decoder happens to apply.
/// </remarks>
internal sealed class SkiaRasterSurface(TypeShaper shaper, ImageExportOptions options) : IPageSink
{
    private readonly Dictionary<OpenTypeFont, SKTypeface> _typefaces = [];
    private readonly Dictionary<RasterImage, SKImage> _images = [];
    private readonly List<byte[]> _pages = [];
    private SKSurface? _surface;

    public IReadOnlyList<byte[]> Pages => _pages;

    private SKCanvas Canvas => _surface?.Canvas
        ?? throw new InvalidOperationException("No page is open. BeginPage must be called before drawing.");

    public void BeginPage(Extent size)
    {
        if (_surface is not null)
            throw new InvalidOperationException("A page is already open. EndPage must be called before the next BeginPage.");

        float scale = options.Resolution / 72f;
        SKImageInfo info = new SKImageInfo(Pixels(size.Width, scale), Pixels(size.Height, scale), SKColorType.Rgba8888, SKAlphaType.Premul);

        _surface = SKSurface.Create(info) ?? throw new InvalidOperationException($"Skia could not allocate a {info.Width}×{info.Height} page.");
        Canvas.Clear(options.Format == PageImageFormat.Jpeg ? SKColors.White : SKColors.Transparent);

        // The page fills the whole pixel grid, as a viewer rendering it at this resolution fills it: scaling by the
        // resolution alone would leave the rounding of the size to drift across the page.
        Canvas.Scale(info.Width / size.Width, info.Height / size.Height);
    }

    public void EndPage()
    {
        SKSurface surface = _surface ?? throw new InvalidOperationException("No page is open. BeginPage must be called before drawing.");
        _surface = null;

        using (surface)
        {
            using SKImage snapshot = surface.Snapshot();
            using SKData data = snapshot.Encode(Encoding(options.Format), options.Quality);
            _pages.Add(data.ToArray());
        }
    }

    public void Save() => Canvas.Save();

    public void Restore() => Canvas.Restore();

    public void Translate(Offset offset) => Canvas.Translate(offset.X, offset.Y);

    public void Scale(float scaleX, float scaleY) => Canvas.Scale(scaleX, scaleY);

    public void Rotate(float degrees) => Canvas.RotateDegrees(degrees);

    public void ClipRectangle(Extent size) => Canvas.ClipRect(SKRect.Create(0, 0, size.Width, size.Height), antialias: true);

    public void DrawRectangle(Offset position, Extent size, Ink color)
    {
        if (color.IsTransparent || size.Width <= 0 || size.Height <= 0)
            return;

        using SKPaint paint = Paint(color);
        Canvas.DrawRect(SKRect.Create(position.X, position.Y, size.Width, size.Height), paint);
    }

    public void DrawRoundedRectangle(Offset position, Extent size, float cornerRadius, Ink color, float strokeWidth = 0f)
    {
        if (color.IsTransparent || size.Width <= 0 || size.Height <= 0)
            return;

        using SKPaint paint = Paint(color);

        if (strokeWidth > 0)
        {
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = strokeWidth;
        }

        // A radius beyond half the shorter side has no shape, so it is clamped, as the PDF surface clamps it.
        float radius = Math.Max(0, Math.Min(cornerRadius, Math.Min(size.Width, size.Height) / 2));
        Canvas.DrawRoundRect(SKRect.Create(position.X, position.Y, size.Width, size.Height), radius, radius, paint);
    }

    public void DrawLine(Offset from, Offset to, float thickness, Ink color, StrokeStyle style = StrokeStyle.Solid)
    {
        if (color.IsTransparent || thickness <= 0)
            return;

        using SKPaint paint = Paint(color);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = thickness;

        switch (style)
        {
            case StrokeStyle.Double:
                Offset shift = StrokeGeometry.DoubleOffset(from, to, thickness);
                Canvas.DrawLine(from.X + shift.X, from.Y + shift.Y, to.X + shift.X, to.Y + shift.Y, paint);
                Canvas.DrawLine(from.X - shift.X, from.Y - shift.Y, to.X - shift.X, to.Y - shift.Y, paint);
                return;

            case StrokeStyle.Dotted:
                // Skia draws no cap on a dash of no length, so the dot is a sliver just long enough to take one.
                paint.StrokeCap = SKStrokeCap.Round;
                paint.PathEffect = SKPathEffect.CreateDash([thickness / 1000, thickness * 2], 0);
                break;

            case StrokeStyle.Dashed:
                paint.PathEffect = SKPathEffect.CreateDash([thickness * 3, thickness * 2], 0);
                break;

            case StrokeStyle.Wavy:
                using (SKPathBuilder builder = new SKPathBuilder())
                {
                    builder.MoveTo(from.X, from.Y);
                    foreach (CubicSegment segment in StrokeGeometry.Wave(from, to, thickness))
                        builder.CubicTo(segment.Control1.X, segment.Control1.Y, segment.Control2.X, segment.Control2.Y, segment.End.X, segment.End.Y);

                    using SKPath wave = builder.Detach();
                    Canvas.DrawPath(wave, paint);
                }

                return;
        }

        using (paint.PathEffect)
            Canvas.DrawLine(from.X, from.Y, to.X, to.Y, paint);
    }

    public void DrawText(string text, Offset baselineStart, TypeStyle style)
    {
        float size = style.EffectivePointSize;
        if (string.IsNullOrEmpty(text) || style.Ink.IsTransparent || size <= 0)
            return;

        using SKTextBlobBuilder builder = new SKTextBlobBuilder();
        List<ushort> glyphs = [];
        List<SKPoint> positions = [];
        OpenTypeFont? face = null;
        float pen = baselineStart.X;
        float previousStep = 0f;
        bool first = true;

        foreach (ShapedGlyph glyph in shaper.Walk(text.AsSpan(), style))
        {
            // The glyph before moved the pen by its advance and any word spacing it carries; tracking and kerning
            // fall between the two.
            if (!first)
                pen += previousStep + style.Tracking + glyph.Kerning;

            // Each face is its own run, as each is its own font in the PDF.
            if (face is not null && !ReferenceEquals(face, glyph.Face))
            {
                AddRun(builder, face, size, glyphs, positions);
                glyphs.Clear();
                positions.Clear();
            }

            face = glyph.Face;
            glyphs.Add(glyph.Glyph);
            positions.Add(new SKPoint(pen, baselineStart.Y));
            previousStep = glyph.Advance + glyph.Extra;
            first = false;
        }

        AddRun(builder, face!, size, glyphs, positions);

        using SKTextBlob? blob = builder.Build();
        using SKPaint paint = Paint(style.Ink);
        if (blob is not null)
            Canvas.DrawText(blob, 0, 0, paint);
    }

    public void DrawImage(IImage image, Extent size)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image is not RasterImage raster)
        {
            throw new ArgumentException(
                $"Only images loaded with {nameof(RasterImage)} can be drawn; this is a {image.GetType().Name}.",
                nameof(image));
        }

        if (size.Width <= 0 || size.Height <= 0)
            return;

        SKImage decoded = Decode(raster);
        SKCanvas canvas = Canvas;

        canvas.Save();
        canvas.Concat(Placement(raster.Orientation, decoded.Width, decoded.Height, size.Width, size.Height));

        using SKPaint paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(decoded, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
        canvas.Restore();
    }

    // Links and anchors are for readers that can follow them; an image has none.
    public void DrawExternalLink(string url, Extent size)
    {
    }

    public void DrawInternalLink(string destinationName, Extent size)
    {
    }

    public void DrawDestination(string destinationName)
    {
    }

    public void Dispose()
    {
        _surface?.Dispose();
        _surface = null;

        foreach (SKImage image in _images.Values)
            image.Dispose();

        foreach (SKTypeface typeface in _typefaces.Values)
            typeface.Dispose();

        _images.Clear();
        _typefaces.Clear();
    }

    /// <summary>
    /// Maps the stored image, in its own pixels, onto the box so that it stands upright: each EXIF orientation carries
    /// the stored corners to where the upright image has them, as <c>PdfSurface.Placement</c> does for PDF.
    /// </summary>
    internal static SKMatrix Placement(ExifOrientation orientation, float storedWidth, float storedHeight, float width, float height)
    {
        float across = width / storedWidth;
        float down = height / storedHeight;
        float acrossFromRows = width / storedHeight;
        float downFromColumns = height / storedWidth;

        return orientation switch
        {
            ExifOrientation.FlipHorizontal => new SKMatrix(-across, 0, width, 0, down, 0, 0, 0, 1),
            ExifOrientation.Rotate180 => new SKMatrix(-across, 0, width, 0, -down, height, 0, 0, 1),
            ExifOrientation.FlipVertical => new SKMatrix(across, 0, 0, 0, -down, height, 0, 0, 1),
            ExifOrientation.Transpose => new SKMatrix(0, acrossFromRows, 0, downFromColumns, 0, 0, 0, 0, 1),
            ExifOrientation.Rotate90 => new SKMatrix(0, -acrossFromRows, width, downFromColumns, 0, 0, 0, 0, 1),
            ExifOrientation.Transverse => new SKMatrix(0, -acrossFromRows, width, -downFromColumns, 0, height, 0, 0, 1),
            ExifOrientation.Rotate270 => new SKMatrix(0, acrossFromRows, 0, -downFromColumns, 0, height, 0, 0, 1),
            _ => new SKMatrix(across, 0, 0, 0, down, 0, 0, 0, 1),
        };
    }

    // To the nearest pixel, as PDF viewers size a page at a resolution: A4 at 96 pixels per inch is 794 wide.
    private static int Pixels(float points, float scale) => Math.Max(1, (int)Math.Round(points * scale, MidpointRounding.AwayFromZero));

    private static SKEncodedImageFormat Encoding(PageImageFormat format) => format switch
    {
        PageImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
        PageImageFormat.Webp => SKEncodedImageFormat.Webp,
        _ => SKEncodedImageFormat.Png,
    };

    private static SKPaint Paint(Ink ink)
    {
        // Pixels are RGB: process colours convert without a profile, and spot inks show their fallback.
        (float red, float green, float blue) = ink.ToRgb();

        return new SKPaint
        {
            Color = new SKColor(ToByte(red), ToByte(green), ToByte(blue), ToByte(ink.Opacity)),
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
    }

    private static byte ToByte(float fraction) => (byte)Math.Round(fraction * 255);

    private void AddRun(SKTextBlobBuilder builder, OpenTypeFont face, float size, List<ushort> glyphs, List<SKPoint> positions)
    {
        using SKFont font = new SKFont(TypefaceFor(face), size)
        {
            Hinting = SKFontHinting.None,
            LinearMetrics = true,
            Subpixel = true,
            Edging = SKFontEdging.Antialias,
        };

        builder.AddPositionedRun(glyphs.ToArray(), font, positions.ToArray());
    }

    private SKTypeface TypefaceFor(OpenTypeFont face)
    {
        if (!_typefaces.TryGetValue(face, out SKTypeface? typeface))
        {
            using SKData data = SKData.CreateCopy(face.FileData.Span);
            typeface = SKTypeface.FromData(data, face.FaceIndex)
                ?? throw new InvalidOperationException($"Skia could not load the face {face.Names.FullName}.");
            _typefaces.Add(face, typeface);
        }

        return typeface;
    }

    /// <summary>Decodes the pixels as stored, without any orientation applied: the placement turns them.</summary>
    private SKImage Decode(RasterImage image)
    {
        if (_images.TryGetValue(image, out SKImage? decoded))
            return decoded;

        using SKData data = SKData.CreateCopy(image.Source.Span);
        using SKCodec codec = SKCodec.Create(data)
            ?? throw new ArgumentException("Skia could not decode the image.", nameof(image));

        SKImageInfo info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKBitmap bitmap = new SKBitmap(info);
        SKCodecResult result = codec.GetPixels(info, bitmap.GetPixels());

        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            throw new ArgumentException($"Skia could not decode the image: {result}.", nameof(image));

        decoded = SKImage.FromBitmap(bitmap);
        _images.Add(image, decoded);
        return decoded;
    }
}
