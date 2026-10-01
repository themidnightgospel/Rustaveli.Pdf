using SkiaSharp;

namespace Rustaveli.Pdf.Raster;

/// <summary>
/// Pages written as SVG documents, a unit to the point, with text as outlines so a page shows the same without its
/// fonts, and images carried within it.
/// </summary>
internal sealed class SvgPageTarget : ISkiaPageTarget
{
    private SKDynamicMemoryWStream? _stream;
    private SKCanvas? _canvas;

    public SKCanvas Begin(Extent size, out SKPoint unitsPerPoint)
    {
        if (_canvas is not null)
            throw new InvalidOperationException("A page is already open. EndPage must be called before the next BeginPage.");

        _stream = new SKDynamicMemoryWStream();
        _canvas = SKSvgCanvas.Create(SKRect.Create(size.Width, size.Height), _stream);
        unitsPerPoint = new SKPoint(1, 1);
        return _canvas;
    }

    public byte[] End()
    {
        SKCanvas canvas = _canvas ?? throw new InvalidOperationException("No page is open. BeginPage must be called before drawing.");
        SKDynamicMemoryWStream stream = _stream!;
        _canvas = null;
        _stream = null;

        // The document is complete once its canvas is gone.
        canvas.Dispose();

        using (stream)
        {
            using SKData data = stream.DetachAsData();
            return data.ToArray();
        }
    }

    public bool TextAsOutlines => true;

    public bool IsPixels => false;

    public void Dispose()
    {
        _canvas?.Dispose();
        _stream?.Dispose();
        _canvas = null;
        _stream = null;
    }
}
