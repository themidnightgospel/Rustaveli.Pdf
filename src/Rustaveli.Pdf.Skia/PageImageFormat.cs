namespace Rustaveli.Pdf;

/// <summary>
/// The file format pages are exported as images in.
/// </summary>
public enum PageImageFormat
{
    /// <summary>Lossless, with transparency where no paper is set.</summary>
    Png,

    /// <summary>Lossy and compact, on white.</summary>
    Jpeg,

    /// <summary>Lossy and more compact still, with transparency.</summary>
    Webp,
}
