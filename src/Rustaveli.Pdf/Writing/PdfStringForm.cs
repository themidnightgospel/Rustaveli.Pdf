namespace Rustaveli.Pdf.Writing;

/// <summary>Which of PDF's two string syntaxes a string is written in. Both denote the same bytes.</summary>
internal enum PdfStringForm
{
    /// <summary><c>(text)</c>, with parentheses, backslashes and control characters escaped.</summary>
    Literal,

    /// <summary><c>&lt;74657874&gt;</c>, two hexadecimal digits per byte.</summary>
    Hex,
}
