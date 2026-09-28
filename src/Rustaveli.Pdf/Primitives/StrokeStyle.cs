namespace Rustaveli.Pdf;

/// <summary>
/// How a stroke is drawn — the stroke types of a page-layout application.
/// </summary>
public enum StrokeStyle
{
    /// <summary>One continuous line.</summary>
    Solid,

    /// <summary>Two lines, each the stroke's weight, a weight apart and centred on where one would be.</summary>
    Double,

    /// <summary>Round dots, each as wide as the stroke.</summary>
    Dotted,

    /// <summary>Dashes three times the stroke's weight, with gaps of twice it.</summary>
    Dashed,

    /// <summary>A wave, as a spelling checker marks a word.</summary>
    Wavy,
}
