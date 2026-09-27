namespace Rustaveli.Pdf.Writing;

/// <summary>How a file locates its objects.</summary>
internal enum PdfCrossReferenceFormat
{
    /// <summary>
    /// A compressed cross-reference stream (PDF 1.5), with every object that is not itself a stream packed into
    /// compressed object streams. The compact form: dictionaries such as pages and annotations are small and highly
    /// repetitive, and compress well together.
    /// </summary>
    Stream,

    /// <summary>
    /// A classic cross-reference table, one twenty-byte line per object, with every object written as plain text.
    /// Larger, but readable by any PDF consumer however old, and easy to inspect by eye.
    /// </summary>
    Table,
}
