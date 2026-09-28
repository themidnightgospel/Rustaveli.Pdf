using Rustaveli.Pdf.Fonts.Substitution;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// The OpenType script a character is written in, as far as choosing a face's substitutions needs to know.
/// </summary>
/// <remarks>
/// A font keeps its features per script, and most keep the same ones under <c>DFLT</c> and <c>latn</c>, so this
/// recognises by block only the scripts fonts commonly treat apart. Characters used by every script — digits,
/// spaces, punctuation — belong to none and take the script of the text around them; anything unrecognised is set
/// with the font's default script, which the substitution table falls back to anyway.
/// </remarks>
internal static class ScriptDetection
{
    /// <summary>The script of the first character of <paramref name="text"/> that has one; <c>DFLT</c> when none has.</summary>
    public static ScriptTag Of(ReadOnlySpan<char> text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            int codepoint = text[index];

            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                codepoint = char.ConvertToUtf32(text[index], text[++index]);

            if (Of(codepoint) is ScriptTag script)
                return script;
        }

        return ScriptTag.Default;
    }

    /// <summary>The script of one character, or null for one every script shares or one not recognised.</summary>
    public static ScriptTag? Of(int codepoint) => codepoint switch
    {
        >= 'A' and <= 'Z' or >= 'a' and <= 'z' => ScriptTag.Latin,
        >= 0x00C0 and <= 0x024F and not (0x00D7 or 0x00F7) => ScriptTag.Latin,
        >= 0x1E00 and <= 0x1EFF or >= 0x2C60 and <= 0x2C7F or >= 0xA720 and <= 0xA7FF => ScriptTag.Latin,
        >= 0xAB30 and <= 0xAB6F or >= 0xFB00 and <= 0xFB06 => ScriptTag.Latin,
        >= 0xFF21 and <= 0xFF3A or >= 0xFF41 and <= 0xFF5A => ScriptTag.Latin,
        >= 0x0370 and <= 0x03FF or >= 0x1F00 and <= 0x1FFF => ScriptTag.Greek,
        >= 0x0400 and <= 0x052F or >= 0x1C80 and <= 0x1C8F or >= 0x2DE0 and <= 0x2DFF => ScriptTag.Cyrillic,
        >= 0xA640 and <= 0xA69F => ScriptTag.Cyrillic,
        >= 0x0530 and <= 0x058F or >= 0xFB13 and <= 0xFB17 => ScriptTag.Armenian,
        >= 0x0590 and <= 0x05FF or >= 0xFB1D and <= 0xFB4F => ScriptTag.Hebrew,
        >= 0x0600 and <= 0x06FF or >= 0x0750 and <= 0x077F or >= 0x08A0 and <= 0x08FF => ScriptTag.Arabic,
        >= 0xFB50 and <= 0xFDFF or >= 0xFE70 and <= 0xFEFF => ScriptTag.Arabic,
        >= 0x10A0 and <= 0x10FF or >= 0x1C90 and <= 0x1CBF or >= 0x2D00 and <= 0x2D2F => ScriptTag.Georgian,
        _ => null,
    };
}
