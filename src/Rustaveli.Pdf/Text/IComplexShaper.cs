using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// Shapes text in the scripts whose shaping needs rules of their own — Arabic joining, Indic reordering and the
/// like — in place of the core's glyph substitution. The Rustaveli.Pdf.Shaping package provides one; without it
/// such text is set glyph for glyph (see ADR 0015).
/// </summary>
internal interface IComplexShaper
{
    /// <summary>Whether a run of text set in one face needs this shaper.</summary>
    bool Handles(ReadOnlySpan<char> run);

    /// <summary>
    /// Shapes a run set in <paramref name="face"/>, adding its glyphs to <paramref name="output"/> in logical order:
    /// cluster by cluster as the text reads, each cluster's glyphs as they are drawn.
    /// </summary>
    /// <param name="face">The face the whole run is set in.</param>
    /// <param name="run">The run, in logical order.</param>
    /// <param name="pointSize">The size it is set at.</param>
    /// <param name="features">The features the style turns on or off beyond the shaper's defaults.</param>
    /// <param name="rightToLeft">
    /// Whether the run is displayed right to left, in which case the shaper mirrors the characters that have a mirror
    /// image, such as brackets: the run is handed over as typed, not mirrored already. Otherwise the run is shaped in
    /// the direction its script is written in, which is how text is measured before its direction is known.
    /// </param>
    /// <param name="output">Receives the glyphs, clusters counted from the start of <paramref name="run"/>.</param>
    void Shape(
        OpenTypeFont face, ReadOnlySpan<char> run, float pointSize, TypeFeatures features, bool rightToLeft, List<ComplexGlyph> output);
}
