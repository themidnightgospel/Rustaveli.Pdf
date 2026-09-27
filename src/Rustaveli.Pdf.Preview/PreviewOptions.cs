namespace Rustaveli.Pdf;

/// <summary>How a document is previewed.</summary>
public sealed class PreviewOptions
{
    private float _resolution = 144;

    /// <summary>The port the preview is served on, or 0, the default, for any free one.</summary>
    public int Port { get; set; }

    /// <summary>The resolution pages are drawn at, in pixels per inch: 144 unless set, sharp on most screens.</summary>
    public float Resolution
    {
        get => _resolution;
        set
        {
            if (!(value > 0) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "A resolution is a finite number of pixels per inch above nothing.");

            _resolution = value;
        }
    }

    /// <summary>Whether the preview opens in the system's browser as it starts; on by default.</summary>
    public bool OpenBrowser { get; set; } = true;

    /// <summary>The typefaces text is set in. <see cref="TypefaceLibrary.Shared"/> when not set.</summary>
    public TypefaceLibrary? Typefaces { get; set; }
}
