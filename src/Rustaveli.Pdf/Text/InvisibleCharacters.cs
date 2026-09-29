namespace Rustaveli.Pdf.Text;

/// <summary>
/// The characters that are never drawn: control characters, and the format characters Unicode says to ignore where
/// they cannot be shown (Default_Ignorable_Code_Point) — joiners, direction marks and isolates, variation selectors,
/// the byte order mark and the like.
/// </summary>
/// <remarks>
/// A face without a glyph for one sets it as nothing, rather than as its missing-glyph box, and it goes with the
/// character before it: set in that character's face, so a selector or joiner does not split a run of it, and read
/// back as part of it. A tab, the one control character typed as spacing, is set as a space instead.
/// </remarks>
internal static class InvisibleCharacters
{
    public const int Tab = '\t';

    /// <summary>Whether <paramref name="codepoint"/> is drawn as nothing when its face has no glyph for it.</summary>
    /// <remarks>
    /// The Hangul fillers are default ignorable but left out, as HarfBuzz leaves them out: fonts draw them as spacing
    /// glyphs.
    /// </remarks>
    public static bool Contains(int codepoint) => codepoint switch
    {
        < 0x20 => codepoint != Tab,
        >= 0x7F and <= 0x9F => true,
        < 0x00AD => false,
        0x00AD or 0x034F or 0x061C => true,
        >= 0x17B4 and <= 0x17B5 => true,
        >= 0x180B and <= 0x180F => true,
        >= 0x200B and <= 0x200F => true,
        >= 0x202A and <= 0x202E => true,
        >= 0x2060 and <= 0x206F => true,
        >= 0xFE00 and <= 0xFE0F => true,
        0xFEFF => true,
        >= 0xFFF0 and <= 0xFFF8 => true,
        >= 0x1D173 and <= 0x1D17A => true,
        >= 0xE0000 and <= 0xE0FFF => true,
        _ => false,
    };
}
