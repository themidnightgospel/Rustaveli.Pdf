namespace Rustaveli.Pdf.Fonts;

/// <summary>How the character codes of the character map a font is read through relate to Unicode.</summary>
internal enum CharacterEncoding
{
    /// <summary>The font has no character map this library can read; nothing maps.</summary>
    None,

    /// <summary>Codes are Unicode code points.</summary>
    Unicode,

    /// <summary>
    /// A Windows symbol font such as Wingdings: codes sit in U+F000 to U+F0FF, and a byte-sized character is looked
    /// up at U+F0xx, as Windows itself does.
    /// </summary>
    Symbol,

    /// <summary>Codes are Mac OS Roman bytes, from a font made only for the classic Macintosh.</summary>
    MacRoman
}
