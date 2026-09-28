namespace Rustaveli.Pdf.Text;

/// <summary>
/// Vertical metrics of a font at a specific size, in PDF points.
/// </summary>
/// <param name="Ascent">Distance from the baseline up to the top of the tallest glyph, positive.</param>
/// <param name="Descent">Distance from the baseline down to the bottom of the lowest glyph, positive.</param>
/// <param name="LineGap">Recommended extra leading between consecutive lines.</param>
/// <param name="UnderlineOffset">How far below the baseline an underline's centre falls; 0 when the font does not say.</param>
/// <param name="UnderlineWeight">How thick the font draws an underline; 0 when it does not say.</param>
/// <param name="StrikeHeight">How far above the baseline a strike-through's centre falls; 0 when the font does not say.</param>
/// <param name="StrikeWeight">How thick the font draws a strike-through; 0 when it does not say.</param>
internal readonly record struct TypeMetrics(
    float Ascent,
    float Descent,
    float LineGap,
    float UnderlineOffset = 0f,
    float UnderlineWeight = 0f,
    float StrikeHeight = 0f,
    float StrikeWeight = 0f)
{
    /// <summary>Total height occupied by one line of this font.</summary>
    public float LineSpacing => Ascent + Descent + LineGap;
}
