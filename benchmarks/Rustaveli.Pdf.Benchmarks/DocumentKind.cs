namespace Rustaveli.Pdf.Benchmarks;

/// <summary>The fixed set of documents both libraries are measured on.</summary>
public enum DocumentKind
{
    /// <summary>One page: a header block, a twenty-line item table and totals. Dominated by fixed per-document cost.</summary>
    Invoice,

    /// <summary>About a hundred pages of flowing paragraphs under a running header and page-numbered footer.</summary>
    Report,

    /// <summary>A ten-thousand-row table with a repeating header band. Stresses pagination and cell layout.</summary>
    LargeTable,

    /// <summary>Twenty pages of photographs. Stresses image decoding, scaling and embedding.</summary>
    Images
}
