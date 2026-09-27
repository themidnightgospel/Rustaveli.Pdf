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

    /// <summary>The shader shapes are painted with instead of their ink, while a gradient is set.</summary>
    private SKShader? _gradient;

    /// <summary>How many pixels make a point across and down the page being drawn.</summary>
    private SKPoint _pixelsPerPoint = new SKPoint(1, 1);

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
        _pixelsPerPoint = new SKPoint(info.Width / size.Width, info.Height / size.Height);
        Canvas.Scale(_pixelsPerPoint.X, _pixelsPerPoint.Y);
    }

    public void EndPage()
    {
        SKSurface surface = _surface ?? throw new InvalidOperationException("No page is open. BeginPage must be called before drawing.");
        _surface = null;
        EndGradient();

        using (surface)
        {
            using SKImage snapshot = surface.Snapshot();
            using SKData data = snapshot.Encode(Encoding(options.Format), options.Quality);
            _pages.Add(data.ToArray());
        }
    }

    public Offset Origin
    {
        get
        {
            // The canvas maps points to pixels; the page's own scale is taken back off.
            SKMatrix matrix = Canvas.TotalMatrix;
            return new Offset(matrix.TransX / _pixelsPerPoint.X, matrix.TransY / _pixelsPerPoint.Y);
        }
    }

    public void Save() => Canvas.Save();

    public void Restore() => Canvas.Restore();

    public void Translate(Offset offset) => Canvas.Translate(offset.X, offset.Y);

    public void Scale(float scaleX, float scaleY) => Canvas.Scale(scaleX, scaleY);

    public void Rotate(float degrees) => Canvas.RotateDegrees(degrees);

    public void Concatenate(float a, float b, float c, float d, float e, float f)
    {
        SKMatrix matrix = new SKMatrix(a, c, e, b, d, f, 0, 0, 1);
        Canvas.Concat(in matrix);
    }

    public void FillPath(VectorPath path, Ink ink, FillRule rule)
    {
        if (ink.IsTransparent || path.IsEmpty)
            return;

        using SKPaint paint = ShapePaint(ink);
        using SKPath shape = ToSkia(path, rule);
        Canvas.DrawPath(shape, paint);
    }

    public void StrokePath(VectorPath path, Ink ink, LineStyle style)
    {
        if (ink.IsTransparent || path.IsEmpty || style.Weight <= 0)
            return;

        using SKPaint paint = ShapePaint(ink);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = style.Weight;
        paint.StrokeCap = style.Cap switch { LineCap.Round => SKStrokeCap.Round, LineCap.Square => SKStrokeCap.Square, _ => SKStrokeCap.Butt };
        paint.StrokeJoin = style.Join switch { LineJoin.Round => SKStrokeJoin.Round, LineJoin.Bevel => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter };
        paint.StrokeMiter = Math.Max(1, style.MiterLimit);

        if (style.Dashes is { Count: > 0 } dashes && dashes.Any(length => length > 0))
        {
            // Skia needs an even number of intervals; a pattern of odd length repeats twice over to make one, as PDF's does.
            float[] intervals = new float[dashes.Count % 2 == 0 ? dashes.Count : dashes.Count * 2];
            for (int index = 0; index < intervals.Length; index++)
                intervals[index] = Math.Max(0, dashes[index % dashes.Count]);

            paint.PathEffect = SKPathEffect.CreateDash(intervals, style.DashOffset);
        }

        using SKPath shape = ToSkia(path, FillRule.NonZero);

        using (paint.PathEffect)
            Canvas.DrawPath(shape, paint);
    }

    public void ClipPath(VectorPath path, FillRule rule)
    {
        using SKPath shape = ToSkia(path, rule);
        Canvas.ClipPath(shape, SKClipOperation.Intersect, antialias: true);
    }

    private static SKPath ToSkia(VectorPath path, FillRule rule)
    {
        using SKPathBuilder builder = new SKPathBuilder();
        builder.FillType = rule == FillRule.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        IReadOnlyList<Offset> points = path.Points;
        int point = 0;

        foreach (PathVerb verb in path.Verbs)
        {
            switch (verb)
            {
                case PathVerb.Move:
                    builder.MoveTo(points[point].X, points[point].Y);
                    point++;
                    break;

                case PathVerb.Line:
                    builder.LineTo(points[point].X, points[point].Y);
                    point++;
                    break;

                case PathVerb.Cubic:
                    builder.CubicTo(points[point].X, points[point].Y, points[point + 1].X, points[point + 1].Y, points[point + 2].X, points[point + 2].Y);
                    point += 3;
                    break;

                default:
                    builder.Close();
                    break;
            }
        }

        return builder.Detach();
    }

    public void ClipRectangle(Extent size) => Canvas.ClipRect(SKRect.Create(0, 0, size.Width, size.Height), antialias: true);

    public void DrawRectangle(Offset position, Extent size, Ink color)
    {
        if (color.IsTransparent || size.Width <= 0 || size.Height <= 0)
            return;

        using SKPaint paint = ShapePaint(color);
        Canvas.DrawRect(SKRect.Create(position.X, position.Y, size.Width, size.Height), paint);
    }

    public void DrawRoundedRectangle(Offset position, Extent size, Corners corners, Ink color, float strokeWidth = 0f)
    {
        if (color.IsTransparent || size.Width <= 0 || size.Height <= 0)
            return;

        using SKPaint paint = ShapePaint(color);

        if (strokeWidth > 0)
        {
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = strokeWidth;
        }

        // Fitted as the PDF surface fits them, so both draw the same shape.
        using SKRoundRect shape = RoundRect(position, size, corners.FittedTo(size));
        Canvas.DrawRoundRect(shape, paint);
    }

    public void DrawLine(Offset from, Offset to, float thickness, Ink color, StrokeStyle style = StrokeStyle.Solid)
    {
        if (color.IsTransparent || thickness <= 0)
            return;

        using SKPaint paint = ShapePaint(color);
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

    public void DrawDashedLine(Offset from, Offset to, float thickness, Ink color, IReadOnlyList<float> pattern)
    {
        if (color.IsTransparent || thickness <= 0)
            return;

        // Skia needs an even number of intervals; a pattern of odd length repeats twice over to make one, as PDF's does.
        float[] intervals = new float[pattern.Count % 2 == 0 ? pattern.Count : pattern.Count * 2];
        for (int index = 0; index < intervals.Length; index++)
            intervals[index] = pattern[index % pattern.Count];

        using SKPaint paint = ShapePaint(color);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = thickness;

        using (paint.PathEffect = SKPathEffect.CreateDash(intervals, 0))
            Canvas.DrawLine(from.X, from.Y, to.X, to.Y, paint);
    }

    public void DrawText(string text, Offset baselineStart, TypeStyle style, bool rightToLeft = false)
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

        foreach (ShapedGlyph glyph in shaper.Walk(text.AsSpan(), style, rightToLeft))
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
            positions.Add(new SKPoint(pen + glyph.XOffset, baselineStart.Y - glyph.YOffset));
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

    /// <summary>A page image has no outline to add to.</summary>
    public void DrawBookmark(string title, int level)
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

    public void BeginGradient(Gradient gradient, Offset position, Extent size)
    {
        (Offset start, Offset end) = gradient.Axis(position, size);
        IReadOnlyList<Ink> inks = gradient.Inks;
        SKColor[] colors = new SKColor[inks.Count];

        for (int index = 0; index < colors.Length; index++)
        {
            (float red, float green, float blue) = inks[index].ToRgb();
            colors[index] = new SKColor(ToByte(red), ToByte(green), ToByte(blue), ToByte(inks[index].Opacity));
        }

        _gradient?.Dispose();
        _gradient = SKShader.CreateLinearGradient(
            new SKPoint(start.X, start.Y), new SKPoint(end.X, end.Y), colors, gradient.Positions.ToArray(), SKShaderTileMode.Clamp);
    }

    public void EndGradient()
    {
        _gradient?.Dispose();
        _gradient = null;
    }

    public void DrawShadow(Offset position, Extent size, Corners corners, Shadow shadow)
    {
        if (shadow.Ink.IsTransparent)
            return;

        (Offset at, Extent grown, Corners radii) = shadow.Shape(position, size, corners);

        if (grown.Width <= 0 || grown.Height <= 0)
            return;

        using SKPaint paint = Paint(shadow.Ink);
        float deviation = shadow.Deviation;

        if (deviation > 0)
            paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, deviation);

        using SKRoundRect shape = RoundRect(at, grown, radii);

        using (paint.MaskFilter)
            Canvas.DrawRoundRect(shape, paint);
    }

    private static SKRoundRect RoundRect(Offset position, Extent size, Corners radii)
    {
        SKRoundRect shape = new SKRoundRect();
        shape.SetRectRadii(
            SKRect.Create(position.X, position.Y, size.Width, size.Height),
            [
                new SKPoint(radii.TopLeft, radii.TopLeft),
                new SKPoint(radii.TopRight, radii.TopRight),
                new SKPoint(radii.BottomRight, radii.BottomRight),
                new SKPoint(radii.BottomLeft, radii.BottomLeft)
            ]);

        return shape;
    }

    /// <summary>The paint for a rectangle, line or outline: its ink, or the gradient set in its place.</summary>
    private SKPaint ShapePaint(Ink ink)
    {
        SKPaint paint = Paint(ink);

        if (_gradient is not null)
        {
            // The shader's colours carry the blend's opacity; the paint's own colour would multiply it again.
            paint.Color = SKColors.White;
            paint.Shader = _gradient;
        }

        return paint;
    }

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
