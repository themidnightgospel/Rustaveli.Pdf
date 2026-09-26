namespace Rustaveli.Pdf.Skia;

/// <summary>
/// Options controlling PDF output.
/// </summary>
public sealed class PdfExportOptions
{
    /// <summary>
    /// Supplies typefaces. Provide one to reuse fonts registered from streams across several documents;
    /// otherwise a provider limited to system fonts is created for the call.
    /// </summary>
    public SkiaFontProvider? Fonts { get; set; }

    /// <summary>
    /// DPI at which Skia rasterises effects it cannot express as PDF vectors. Raising it improves the fidelity
    /// of such effects at the cost of file size.
    /// </summary>
    public float RasterDpi { get; set; } = 300f;

    /// <summary>Emits the XMP metadata, document identifier and output intent required for PDF/A-2b.</summary>
    public bool PdfA { get; set; }

    /// <summary>
    /// Quality used when an image has to be re-encoded, from 0 (smallest) to 100. The default of 101 is Skia's
    /// sentinel for lossless, which stores the image with Flate compression instead of as a JPEG.
    /// </summary>
    /// <remarks>
    /// Images that arrive already JPEG-encoded are passed through untouched whatever this is set to, and images
    /// with an alpha channel always take the lossless path. It therefore only affects opaque images supplied in
    /// another format — the logos, charts, screenshots and barcodes for which lossy artefacts are most visible.
    /// </remarks>
    public int EncodingQuality { get; set; } = 101;

    /// <summary>
    /// Allows this render to run alongside others instead of waiting its turn.
    /// </summary>
    /// <remarks>
    /// Off by default, and enabling it risks silently corrupt output. Skia's PDF backend has process-wide font
    /// state that concurrent renders interfere with: the resulting files are structurally valid and roughly the
    /// right size, but their text extracts as unmapped glyphs. Only turn this on if your own testing shows your
    /// workload is unaffected.
    /// </remarks>
    public bool AllowConcurrentRendering { get; set; }
}
