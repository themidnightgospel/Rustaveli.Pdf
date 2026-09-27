using System.Text;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Raster;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf;

/// <summary>
/// Exports a document as SVG, a document to a page, a unit to the point: text as the outlines of its glyphs, so a page
/// shows the same without its fonts, and images carried within it.
/// </summary>
public static class SvgExport
{
    /// <summary>Exports every page as an SVG document, in page order.</summary>
    public static IReadOnlyList<string> ExportSvg(this Document document, SvgExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        options ??= new SvgExportOptions();
        TypeShaper shaper = (options.Typefaces ?? TypefaceLibrary.Shared).Shaper;

        using SkiaRasterSurface surface = new SkiaRasterSurface(shaper, new SvgPageTarget());
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(shaper);
        Typesetter.Render(document, surface, measurer, options.ImageResolution);

        if (options.RequireEveryGlyph && measurer.MissingCodepoints.Count > 0)
            throw new MissingGlyphException(measurer.MissingCodepoints);

        return surface.Pages.Select(page => Encoding.UTF8.GetString(page)).ToArray();
    }

    /// <summary>Exports every page as an SVG file, at the path <paramref name="pathOfPage"/> gives its number, from 1.</summary>
    public static void ExportSvg(this Document document, Func<int, string> pathOfPage, SvgExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pathOfPage);

        IReadOnlyList<string> pages = document.ExportSvg(options);
        for (int index = 0; index < pages.Count; index++)
            File.WriteAllText(pathOfPage(index + 1), pages[index]);
    }
}
