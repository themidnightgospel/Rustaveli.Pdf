namespace Rustaveli.Pdf.Fonts;

/// <summary>What the GDEF table says a glyph is, which lookup flags use to pass over some kinds of glyph.</summary>
internal enum GlyphClass
{
    /// <summary>Not classified: no lookup flag passes over it.</summary>
    Unclassified = 0,

    /// <summary>A single character, spacing glyph: a letter, a digit, a space.</summary>
    Base = 1,

    /// <summary>A glyph standing for several characters, such as the "fi" ligature.</summary>
    Ligature = 2,

    /// <summary>A non-spacing combining glyph, such as an accent.</summary>
    Mark = 3,

    /// <summary>Part of a character drawn with several glyphs.</summary>
    Component = 4
}
