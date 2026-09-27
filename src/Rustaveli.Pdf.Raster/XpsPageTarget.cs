using SkiaSharp;

namespace Rustaveli.Pdf.Raster;

/// <summary>
/// Pages written into one XPS document, a unit to the point. Skia writes XPS only on Windows, where the system's
/// XPS support does the work.
/// </summary>
internal sealed class XpsPageTarget : ISkiaPageTarget
{
    private readonly SKDynamicMemoryWStream _stream = new SKDynamicMemoryWStream();
    private readonly SKDocument _document;
    private bool _open;

    public XpsPageTarget()
    {
        _document = SKDocument.CreateXps(_stream) ?? throw new PlatformNotSupportedException("XPS can only be written on Windows.");
    }

    public bool TextAsOutlines => false;

    public SKCanvas Begin(Extent size, out SKPoint unitsPerPoint)
    {
        if (_open)
            throw new InvalidOperationException("A page is already open. EndPage must be called before the next BeginPage.");

        _open = true;
        unitsPerPoint = new SKPoint(1, 1);
        return _document.BeginPage(size.Width, size.Height);
    }

    public byte[] End()
    {
        if (!_open)
            throw new InvalidOperationException("No page is open. BeginPage must be called before drawing.");

        _open = false;
        _document.EndPage();

        // The pages make one document, finished all together.
        return [];
    }

    /// <summary>The whole document, once its last page is drawn.</summary>
    public byte[] Finish()
    {
        _document.Close();

        using SKData data = _stream.DetachAsData();
        return data.ToArray();
    }

    public void Dispose()
    {
        _document.Dispose();
        _stream.Dispose();
    }
}
