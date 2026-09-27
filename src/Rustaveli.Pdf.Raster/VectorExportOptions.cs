namespace Rustaveli.Pdf;

/// <summary>How a document is exported as vector pages: SVG, or XPS.</summary>
public sealed class VectorExportOptions
{
    private float _imageResolution = 288;

    /// <summary>The typefaces text is set in. <see cref="TypefaceLibrary.Shared"/> when not set.</summary>
    public TypefaceLibrary? Typefaces { get; set; }

    /// <summary>
    /// Whether every character must be found in some typeface; on, the export fails with
    /// <see cref="MissingGlyphException"/> naming those that are not.
    /// </summary>
    public bool RequireEveryGlyph { get; set; }

    /// <summary>The resolution images generated at their final size are generated at: 288 pixels an inch unless set.</summary>
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
}
