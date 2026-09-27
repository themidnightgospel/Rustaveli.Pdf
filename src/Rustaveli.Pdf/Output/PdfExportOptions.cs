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

    private float _imageResolution = 288;
}
