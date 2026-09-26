namespace Rustaveli.Pdf.Text;

/// <summary>
/// Vertical metrics of a font at a specific size, in PDF points.
/// </summary>
/// <param name="Ascent">Distance from the baseline up to the top of the tallest glyph, positive.</param>
/// <param name="Descent">Distance from the baseline down to the bottom of the lowest glyph, positive.</param>
/// <param name="LineGap">Recommended extra leading between consecutive lines.</param>
public readonly record struct TypeMetrics(float Ascent, float Descent, float LineGap)
{
    /// <summary>Total height occupied by one line of this font.</summary>
    public float LineHeight => Ascent + Descent + LineGap;
}
