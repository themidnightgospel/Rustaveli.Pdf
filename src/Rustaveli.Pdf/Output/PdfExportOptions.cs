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
}
