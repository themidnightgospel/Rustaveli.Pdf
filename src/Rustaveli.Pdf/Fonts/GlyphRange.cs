using System.Diagnostics.CodeAnalysis;

namespace Rustaveli.Pdf.Fonts;

/// <summary>The check that a glyph id a caller passes in names one of the font's glyphs.</summary>
internal static class GlyphRange
{
    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> for <paramref name="paramName"/> unless
    /// <paramref name="glyph"/> is below <paramref name="glyphCount"/>.
    /// </summary>
    public static void Check(ushort glyph, int glyphCount, string paramName)
    {
        if (glyph >= glyphCount)
            ThrowOutside(glyph, glyphCount, paramName);
    }

    // Apart from the check, which runs for every glyph measured, so that it stays small enough to inline and the
    // message is built only when it throws.
    [DoesNotReturn]
    private static void ThrowOutside(ushort glyph, int glyphCount, string paramName) =>
        throw new ArgumentOutOfRangeException(paramName, glyph, $"The font has {glyphCount} glyphs.");
}
