namespace Rustaveli.Pdf;

/// <summary>
/// The part and level of PDF/A, the ISO 19005 standard for documents kept for the long term, a PDF is written to.
/// </summary>
/// <remarks>
/// Every level embeds its fonts, names its colours through an sRGB output intent — inks are written in RGB — and
/// describes itself in XMP metadata. The <c>U</c> levels also map every glyph to the text it shows. Part 3 allows files
/// of any kind to be attached.
/// </remarks>
public enum PdfAConformance
{
    /// <summary>No PDF/A: a plain PDF 1.7.</summary>
    None,

    /// <summary>PDF/A-2b: the pages look the same wherever they are shown.</summary>
    PdfA2B,

    /// <summary>PDF/A-2u: as 2b, and every glyph's text can be read back.</summary>
    PdfA2U,

    /// <summary>PDF/A-3b: as 2b, allowing attached files of any kind.</summary>
    PdfA3B,

    /// <summary>PDF/A-3u: as 2u, allowing attached files of any kind.</summary>
    PdfA3U,
}
