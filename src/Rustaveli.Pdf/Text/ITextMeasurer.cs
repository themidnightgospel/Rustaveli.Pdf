namespace Rustaveli.Pdf.Text;

/// <summary>
/// Supplies the font measurements the layout engine needs before anything is drawn.
/// </summary>
/// <remarks>
/// Kept deliberately narrow so the layout engine stays independent of the rendering backend: an implementation
/// may be backed by a native text shaper or by a pure-managed font parser without the engine noticing.
/// </remarks>
public interface ITextMeasurer
{
    FontMetrics GetMetrics(TextStyle style);

    /// <summary>Width of <paramref name="text" /> laid out on a single line, ignoring wrapping.</summary>
    float MeasureWidth(string text, TextStyle style);

    /// <summary>
    /// The number of characters from the start of <paramref name="text" /> that fit within
    /// <paramref name="maxWidth" />. Returns 0 when not even the first character fits.
    /// </summary>
    int MeasureCharactersFitting(string text, TextStyle style, float maxWidth);
}
