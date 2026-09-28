using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Raster;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf;

/// <summary>
/// Exports a document as XPS, the fixed-layout format Windows prints through. Available on Windows only, where the
/// system writes it.
/// </summary>
public static class XpsExport
{
    /// <summary>Exports the document as one XPS document.</summary>
    /// <exception cref="PlatformNotSupportedException">Not running on Windows.</exception>
    public static byte[] ExportXps(this Document document, VectorExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        options ??= new VectorExportOptions();
        TypeShaper shaper = (options.Typefaces ?? TypefaceLibrary.Shared).Shaper;

        XpsPageTarget target = new XpsPageTarget();

        // The surface disposes the target with itself, so the document is finished first.
        using SkiaRasterSurface surface = new SkiaRasterSurface(shaper, target);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(shaper);
        Typesetter.Render(document, surface, measurer, options.ImageResolution);

        if (options.RequireEveryGlyph && measurer.MissingCodepoints.Count > 0)
            throw new MissingGlyphException(measurer.MissingCodepoints);

        return target.Finish();
    }

    /// <summary>Exports the document as one XPS file at <paramref name="path"/>.</summary>
    /// <exception cref="PlatformNotSupportedException">Not running on Windows.</exception>
    public static void ExportXps(this Document document, string path, VectorExportOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        File.WriteAllBytes(path, document.ExportXps(options));
    }
}
