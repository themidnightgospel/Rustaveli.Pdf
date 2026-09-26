using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;
using SkiaSharp;
using Ink = Rustaveli.Pdf.Primitives.Ink;

namespace Rustaveli.Pdf.Skia;

/// <summary>
/// Writes drawing operations into a PDF using Skia's PDF backend.
/// </summary>
public sealed class SkiaPdfCanvas(SKDocument document, SkiaFontProvider fonts) : IPageSink
{
    private SKCanvas? _canvas;

    private SKCanvas Canvas => _canvas
        ?? throw new InvalidOperationException("No page is open. BeginPage must be called before drawing.");

    public void BeginPage(Extent size) => _canvas = document.BeginPage(size.Width, size.Height);

    public void EndPage()
    {
        document.EndPage();
        _canvas = null;
    }

    public void Save() => Canvas.Save();

    public void Restore() => Canvas.Restore();

    public void Translate(Offset offset) => Canvas.Translate(offset.X, offset.Y);

    public void Scale(float scaleX, float scaleY) => Canvas.Scale(scaleX, scaleY);

    public void Rotate(float degrees) => Canvas.RotateDegrees(degrees);

    public void ClipRectangle(Extent size) => Canvas.ClipRect(SKRect.Create(0, 0, size.Width, size.Height));

    public void DrawRectangle(Offset position, Extent size, Ink color)
    {
        if (color.IsTransparent || size.Width <= 0 || size.Height <= 0)
            return;

        using SKPaint paint = CreatePaint(color);
        Canvas.DrawRect(SKRect.Create(position.X, position.Y, size.Width, size.Height), paint);
    }

    public void DrawRoundedRectangle(Offset position, Extent size, float cornerRadius, Ink color, float strokeWidth = 0f)
    {
        if (color.IsTransparent || size.Width <= 0 || size.Height <= 0)
            return;

        using SKPaint paint = CreatePaint(color);

        if (strokeWidth > 0)
        {
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = strokeWidth;
        }

        // A radius larger than half the shorter side would produce an invalid shape, so clamp it.
        float radius = Math.Max(0, Math.Min(cornerRadius, Math.Min(size.Width, size.Height) / 2));
        SKRect rect = SKRect.Create(position.X, position.Y, size.Width, size.Height);

        Canvas.DrawRoundRect(rect, radius, radius, paint);
    }

    public void DrawLine(Offset from, Offset to, float thickness, Ink color)
    {
        if (color.IsTransparent || thickness <= 0)
            return;

        using SKPaint paint = CreatePaint(color);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = thickness;

        Canvas.DrawLine(from.X, from.Y, to.X, to.Y, paint);
    }

    public void DrawText(string text, Offset baselineStart, TypeStyle style)
    {
        if (string.IsNullOrEmpty(text) || style.Ink.IsTransparent)
            return;

        using SKPaint paint = CreatePaint(style.Ink);

        // Split the same way the measurer did, so a fallback glyph lands exactly where its advance was reserved.
        IReadOnlyList<FontRun> runs = fonts.Split(text, style);
        float x = baselineStart.X;
        int drawnCharacters = 0;

        foreach (FontRun run in runs)
        {
            // The layout engine has already resolved the origin, so text is always drawn left-aligned from it.
            if (style.Tracking == 0)
            {
                Canvas.DrawText(run.Text, x, baselineStart.Y, SKTextAlign.Left, run.Font, paint);
                x += run.Font.MeasureText(run.Text);
                continue;
            }

            // Skia has no tracking setting, so spaced text is emitted one character at a time. Surrogate pairs
            // are kept whole: drawing each half separately renders two unmapped glyphs instead of one character.
            int position = 0;

            while (position < run.Text.Length)
            {
                int length = char.IsSurrogatePair(run.Text, position) ? 2 : 1;
                string glyph = run.Text.Substring(position, length);

                if (drawnCharacters > 0)
                    x += style.Tracking;

                Canvas.DrawText(glyph, x, baselineStart.Y, SKTextAlign.Left, run.Font, paint);

                x += run.Font.MeasureText(glyph);
                position += length;
                drawnCharacters++;
            }
        }
    }

    public void DrawImage(IImage image, Extent size)
    {
        if (image is not SkiaImage skiaImage)
            throw new ArgumentException($"This canvas can only draw images created by {nameof(SkiaImage)}.", nameof(image));

        if (size.Width <= 0 || size.Height <= 0)
            return;

        using SKPaint paint = new SKPaint { IsAntialias = true };
        SKSamplingOptions sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        Canvas.DrawImage(skiaImage.Image, SKRect.Create(0, 0, size.Width, size.Height), sampling, paint);
    }

    public void DrawExternalLink(string url, Extent size)
    {
        if (string.IsNullOrEmpty(url))
            return;

        Canvas.DrawUrlAnnotation(SKRect.Create(0, 0, size.Width, size.Height), url);
    }

    public void DrawInternalLink(string destinationName, Extent size)
    {
        if (string.IsNullOrEmpty(destinationName))
            return;

        Canvas.DrawLinkDestinationAnnotation(SKRect.Create(0, 0, size.Width, size.Height), destinationName);
    }

    public void DrawDestination(string destinationName)
    {
        if (string.IsNullOrEmpty(destinationName))
            return;

        Canvas.DrawNamedDestinationAnnotation(new SKPoint(0, 0), destinationName);
    }

    private static SKPaint CreatePaint(Ink color) => new()
    {
        Color = ToSkColor(color),
        IsAntialias = true,
        Style = SKPaintStyle.Fill
    };

    // Skia draws in RGB only: process colours convert without a profile, and spot inks show their fallback.
    private static SKColor ToSkColor(Ink ink)
    {
        (float red, float green, float blue) = ink.ToRgb();
        return new SKColor(ToByte(red), ToByte(green), ToByte(blue), ToByte(ink.Opacity));
    }

    private static byte ToByte(float fraction) => (byte)Math.Round(fraction * 255);

    /// <summary>
    /// Closes any page still open.
    /// </summary>
    /// <remarks>
    /// A render that fails part-way unwinds with a page still begun. Ending it here keeps the document in a
    /// consistent state for whatever the caller does next. The document itself is not disposed — this canvas
    /// borrows it and does not own its lifetime.
    /// </remarks>
    public void Dispose()
    {
        if (_canvas is null)
            return;

        _canvas = null;

        try
        {
            document.EndPage();
        }
        catch (Exception)
        {
            // Already unwinding from an earlier failure; a secondary fault here would mask the real cause.
        }
    }
}
