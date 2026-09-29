using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// One glyph of set text: the face that draws it, the characters it stands for, and how far it moves the pen.
/// </summary>
/// <param name="Face">The face the glyph comes from: the style's own, or a fallback for a character it lacks.</param>
/// <param name="Glyph">The glyph's index in <paramref name="Face"/>; 0 when no face has the character.</param>
/// <param name="Codepoint">The character the glyph stands for, or the first of those a ligature stands for.</param>
/// <param name="Start">Where the glyph's characters start in the text, in UTF-16 code units.</param>
/// <param name="Length">
/// How many UTF-16 code units the glyph stands for: 1, 2 for a surrogate pair, more for a ligature, and 0 for the
/// second and later glyphs a substitution made of one character.
/// </param>
/// <param name="Advance">The glyph's advance, in points.</param>
/// <param name="Kerning">
/// The pair kerning between the previous glyph and this one, in points, negative to bring them closer. Zero for the
/// first glyph, and between glyphs of different faces.
/// </param>
/// <param name="Extra">
/// Space added after the glyph beyond its advance, in points: the style's word spacing, for a word space.
/// </param>
/// <param name="Text">
/// What the glyph stands for when that is not just <paramref name="Codepoint"/>: a ligature's characters, or empty
/// for a glyph standing for none. Null otherwise, which is almost always.
/// </param>
/// <param name="XOffset">How far the glyph is drawn to the right of the pen, in points, without moving it.</param>
/// <param name="YOffset">How far the glyph is drawn above the baseline, in points: a mark placed on its letter.</param>
/// <param name="Tracking">
/// The style's tracking between the previous glyph and this one, in points, where it falls: before a glyph that
/// begins what a reader sees as a new character, not between letters of a script written joined. Zero for the first
/// glyph and for every other.
/// </param>
internal readonly record struct ShapedGlyph(
    OpenTypeFont Face,
    ushort Glyph,
    int Codepoint,
    int Start,
    int Length,
    float Advance,
    float Kerning,
    float Extra = 0f,
    string? Text = null,
    float XOffset = 0f,
    float YOffset = 0f,
    float Tracking = 0f)
{
    /// <summary>
    /// The text the glyph stands for when read back: <see cref="Codepoint"/> for most glyphs, every character of a
    /// ligature, and nothing for the second and later glyphs a substitution made of one character.
    /// </summary>
    public string ReadsAs => Text ?? char.ConvertFromUtf32(Codepoint);
}
