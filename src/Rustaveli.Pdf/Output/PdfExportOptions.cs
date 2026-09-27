namespace Rustaveli.Pdf;

/// <summary>
/// How a document is exported as PDF.
/// </summary>
public sealed class PdfExportOptions
{
    /// <summary>
    /// The typefaces text is set in. <see cref="TypefaceLibrary.Shared"/> when not set.
    /// </summary>
    public TypefaceLibrary? Typefaces { get; set; }

    /// <summary>
    /// Whether streams are compressed. On by default; off makes content streams readable in a text editor.
    /// </summary>
    public bool Compress { get; set; } = true;

    /// <summary>
    /// Whether every character must be found in some typeface. Off by default, when a character no typeface has is
    /// drawn as a missing-glyph box; on, the export fails with <see cref="MissingGlyphException"/> naming them all.
    /// </summary>
    public bool RequireEveryGlyph { get; set; }

    /// <summary>
    /// The resolution images generated at their final size are generated at, in pixels per inch: 288 unless set,
    /// sharp in print.
    /// </summary>
    public float ImageResolution
    {
        get => _imageResolution;
        set
        {
            if (!(value > 0) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "A resolution is a finite number of pixels per inch above nothing.");

            _imageResolution = value;
        }
    }

    /// <summary>
    /// The quality, from 1 to 100, images are compressed at when they set none of their own, or null, the default, to
    /// embed them as they are. Needs an <see cref="ImageProcessor"/>.
    /// </summary>
    public int? ImageQuality
    {
        get => _imageQuality;
        set
        {
            if (value is < 1 or > 100)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Quality runs from 1 to 100.");

            _imageQuality = value;
        }
    }

    /// <summary>
    /// The most pixels per inch images are embedded at where they are shown, when they set none of their own, or
    /// null, the default, to keep all their pixels. Needs an <see cref="ImageProcessor"/>.
    /// </summary>
    public float? MaximumImageResolution
    {
        get => _maximumImageResolution;
        set
        {
            if (value is { } resolution && (!(resolution > 0) || float.IsInfinity(resolution)))
                throw new ArgumentOutOfRangeException(nameof(value), value, "A resolution is a finite number of pixels per inch above nothing.");

            _maximumImageResolution = value;
        }
    }

    /// <summary>What re-encodes images when a quality or maximum resolution asks for it.</summary>
    public IImageProcessor? ImageProcessor { get; set; }

    /// <summary>
    /// The PDF/A part and level the document is written to, <see cref="PdfAConformance.None"/> unless set. Under PDF/A
    /// every glyph must be found, as if <see cref="RequireEveryGlyph"/> were on, and CMYK images without a colour profile
    /// need an <see cref="ImageProcessor"/> to become RGB.
    /// </summary>
    public PdfAConformance Conformance { get; set; }

    /// <summary>
    /// Whether the PDF records the document's structure — its headings, paragraphs, lists, tables and figures, in
    /// reading order — for screen readers, reflow and search. Off by default; PDF/UA and the accessible levels of PDF/A
    /// turn it on.
    /// </summary>
    /// <remarks>
    /// Text is tagged a paragraph where nothing else tags it, lists and links are tagged without asking, and
    /// <see cref="FrameModifiers.Tagged"/> tags the rest. Running heads and feet, and images and drawings outside a
    /// figure, are decoration outside the structure.
    /// </remarks>
    public bool Tagged { get; set; }

    /// <summary>
    /// The accessibility standard the document claims to meet, <see cref="PdfUAConformance.None"/> unless set. PDF/UA
    /// tags the document, and needs its <see cref="DocumentInfo.Title"/> and <see cref="DocumentInfo.Language"/>.
    /// </summary>
    public PdfUAConformance Accessibility { get; set; }

    /// <summary>Whether the structure is written: asked for, or needed by the standard claimed.</summary>
    internal bool WritesStructure =>
        Tagged || Accessibility != PdfUAConformance.None || Conformance is PdfAConformance.PdfA2A or PdfAConformance.PdfA3A;

    private int? _imageQuality;

    private float? _maximumImageResolution;

    private float _imageResolution = 288;
}
