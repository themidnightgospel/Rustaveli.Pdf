namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// The flags of a GSUB or GPOS lookup, chiefly which glyphs it passes over as though they were not there — so that
/// "f", an accent and "i" still form the "fi" ligature, with the accent kept after it.
/// </summary>
[Flags]
internal enum LookupFlags
{
    None = 0,

    /// <summary>Cursive attachment runs right to left; meaningful to GPOS alone.</summary>
    RightToLeft = 0x0001,

    IgnoreBaseGlyphs = 0x0002,

    IgnoreLigatures = 0x0004,

    IgnoreMarks = 0x0008,

    /// <summary>Marks outside the lookup's mark glyph set are passed over.</summary>
    UseMarkFilteringSet = 0x0010,

    /// <summary>
    /// When not zero, the mark attachment class whose marks the lookup sees; marks of every other class are passed
    /// over.
    /// </summary>
    MarkAttachmentType = 0xFF00
}
