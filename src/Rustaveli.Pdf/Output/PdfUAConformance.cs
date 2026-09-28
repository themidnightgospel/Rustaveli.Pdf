namespace Rustaveli.Pdf;

/// <summary>
/// The part of PDF/UA, the ISO 14289 standard for documents everyone can read, assistive technology included, a PDF
/// is written to.
/// </summary>
/// <remarks>
/// A PDF/UA document is tagged, so its structure and reading order are recorded; shows its title rather than its file
/// name; names its language; embeds its fonts; and says what its figures and links are. Tagging content well — headings
/// in order, figures described — is still the author's part.
/// </remarks>
public enum PdfUAConformance
{
    /// <summary>No claim of accessibility.</summary>
    None,

    /// <summary>PDF/UA-1, ISO 14289-1.</summary>
    PdfUA1,
}
