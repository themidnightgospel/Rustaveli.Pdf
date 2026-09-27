using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Raster;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf;

/// <summary>
/// Exports a document's pages as images, drawn by SkiaSharp.
/// </summary>
/// <remarks>
/// Pages are laid out exactly as for PDF: the same typefaces, the same glyphs at the same positions, the same page
/// breaks. Links and anchors have no meaning in an image and are left out.
/// </remarks>
public static class ImageExport
{
    /// <summary>Exports every page as an image, in page order.</summary>
    public static IReadOnlyList<byte[]> ExportImages(this Document document, ImageExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        options ??= new ImageExportOptions();
        TypeShaper shaper = (options.Typefaces ?? TypefaceLibrary.Shared).Shaper;

        using SkiaRasterSurface surface = new SkiaRasterSurface(shaper, options);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(shaper);
        // Images generated at their final size are generated at the page's own resolution.
        Typesetter.Render(document, surface, measurer, options.Resolution);

        if (options.RequireEveryGlyph && measurer.MissingCodepoints.Count > 0)
            throw new MissingGlyphException(measurer.MissingCodepoints);

        return surface.Pages;
    }

    /// <summary>
    /// Exports every page as an image file, at the path <paramref name="pathOfPage"/> gives for its page number,
    /// counted from 1.
    /// </summary>
    public static void ExportImages(this Document document, Func<int, string> pathOfPage, ImageExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pathOfPage);

        IReadOnlyList<byte[]> pages = document.ExportImages(options);
        for (int index = 0; index < pages.Count; index++)
            File.WriteAllBytes(pathOfPage(index + 1), pages[index]);
    }
}
