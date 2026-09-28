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
    /// <param name="output">Receives the glyphs, clusters counted from the start of <paramref name="run"/>.</param>
    void Shape(OpenTypeFont face, ReadOnlySpan<char> run, float pointSize, TypeFeatures features, List<ComplexGlyph> output);
}

/// <summary>A glyph a complex shaper set: where its cluster starts in the run, how far it moves the pen, and where it is drawn.</summary>
/// <param name="Glyph">The glyph's index in the face.</param>
/// <param name="Cluster">Where the characters it stands for start in the run, in UTF-16 code units.</param>
/// <param name="Advance">How far it moves the pen, in points, kerning included.</param>
/// <param name="XOffset">How far right of the pen it is drawn, in points.</param>
/// <param name="YOffset">How far above the baseline it is drawn, in points.</param>
internal readonly record struct ComplexGlyph(ushort Glyph, int Cluster, float Advance, float XOffset, float YOffset);
