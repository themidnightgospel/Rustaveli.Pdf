namespace Rustaveli.Pdf;

/// <summary>
/// How pages are exported as images.
/// </summary>
public sealed class ImageExportOptions
{
    private float _resolution = 144f;
    private int _quality = 90;

    /// <summary>Pixels per inch. 144 by default: twice a screen's 72 points per inch, sharp on most displays.</summary>
    public float Resolution
    {
        get => _resolution;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0f);
            _resolution = value;
        }
    }

    /// <summary>The image format. PNG by default.</summary>
    public PageImageFormat Format { get; set; } = PageImageFormat.Png;

    /// <summary>For JPEG and WebP, the encoder's quality from 1 to 100. 90 by default; PNG ignores it.</summary>
    public int Quality
    {
        get => _quality;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
            _quality = value;
        }
    }

    /// <summary>The typefaces text is set in. <see cref="TypefaceLibrary.Shared"/> when not set.</summary>
    public TypefaceLibrary? Typefaces { get; set; }

    /// <summary>
    /// Whether every character must be found in some typeface. Off by default, when a character no typeface has is
    /// drawn as a missing-glyph box; on, the export fails with <see cref="MissingGlyphException"/> naming them all.
    /// </summary>
    public bool RequireEveryGlyph { get; set; }
}
