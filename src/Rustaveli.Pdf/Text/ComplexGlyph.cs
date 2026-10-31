namespace Rustaveli.Pdf.Text;

/// <summary>
/// A glyph a complex shaper set: where its cluster starts in the run, how far it moves the pen, and where it is drawn.
/// </summary>
/// <param name="Glyph">The glyph's index in the face.</param>
/// <param name="Cluster">Where the characters it stands for start in the run, in UTF-16 code units.</param>
/// <param name="Advance">How far it moves the pen, in points, kerning included.</param>
/// <param name="XOffset">How far right of the pen it is drawn, in points.</param>
/// <param name="YOffset">How far above the baseline it is drawn, in points.</param>
internal readonly record struct ComplexGlyph(ushort Glyph, int Cluster, float Advance, float XOffset, float YOffset);
