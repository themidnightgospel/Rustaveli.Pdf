namespace Rustaveli.Pdf.Fonts;

/// <summary>How a font describes its glyph shapes, which decides how it can be embedded in a PDF.</summary>
internal enum OutlineFormat
{
    /// <summary>
    /// No outline table this library can embed: a bitmap-only or colour-bitmap font such as an emoji font. It can
    /// be measured but not drawn into a PDF.
    /// </summary>
    None,

    /// <summary>
    /// Quadratic TrueType outlines in <c>glyf</c>, embedded as a CIDFontType2 with a subset font program.
    /// </summary>
    TrueType,

    /// <summary>
    /// Cubic PostScript outlines in a <c>CFF </c> table. Not subset by this library: the whole font is embedded
    /// (FontFile3 with subtype OpenType, or the bare CFF table as CIDFontType0C).
    /// </summary>
    Cff,

    /// <summary>
    /// Outlines in a <c>CFF2</c> table, the variable-font successor to CFF. PDF has no font file type for it, so
    /// it cannot be embedded without converting the outlines first.
    /// </summary>
    Cff2
}
