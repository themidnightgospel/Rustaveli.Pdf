using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// One glyph of set text: the face that draws it, the characters it stands for, and how far it moves the pen.
/// </summary>
/// <param name="Face">The face the glyph comes from: the style's own, or a fallback for a character it lacks.</param>
/// <param name="Glyph">The glyph's index in <paramref name="Face"/>; 0 when no face has the character.</param>
/// <param name="Codepoint">The character the glyph stands for.</param>
/// <param name="Start">Where the character starts in the text, in UTF-16 code units.</param>
/// <param name="Length">The character's length in UTF-16 code units: 2 for a surrogate pair, else 1.</param>
/// <param name="Advance">The glyph's advance, in points.</param>
/// <param name="Kerning">
/// The pair kerning between the previous glyph and this one, in points, negative to bring them closer. Zero for the
/// first glyph, and between glyphs of different faces.
/// </param>
internal readonly record struct ShapedGlyph(
    OpenTypeFont Face, ushort Glyph, int Codepoint, int Start, int Length, float Advance, float Kerning);
