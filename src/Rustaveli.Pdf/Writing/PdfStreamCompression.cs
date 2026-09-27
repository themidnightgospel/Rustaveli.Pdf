namespace Rustaveli.Pdf.Writing;

/// <summary>How <see cref="PdfFileWriter"/> treats a stream's data.</summary>
internal enum PdfStreamCompression
{
    /// <summary>
    /// Apply FlateDecode when the stream is long enough to benefit and compression actually makes it smaller. The
    /// dictionary must not name a filter of its own.
    /// </summary>
    Auto,

    /// <summary>
    /// Write the data exactly as given. For data already encoded — a JPEG under <c>/DCTDecode</c>, PNG image data
    /// under <c>/FlateDecode</c> — whose <c>/Filter</c> the caller sets.
    /// </summary>
    None,
}
