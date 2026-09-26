using System.IO.Compression;

namespace Rustaveli.Pdf.Writing;

/// <summary>Choices about the shape of a written file that do not change what it displays.</summary>
internal sealed class PdfWriterOptions
{
    public PdfCrossReferenceFormat CrossReferenceFormat { get; init; } = PdfCrossReferenceFormat.Stream;

    /// <summary>
    /// The deflate effort for streams. <see cref="System.IO.Compression.CompressionLevel.NoCompression"/> writes every
    /// stream uncompressed, which makes content streams readable in a text editor.
    /// </summary>
    public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;

    /// <summary>
    /// The 16 bytes written as both halves of the trailer's <c>/ID</c>. When null, the ID is derived from a hash of
    /// the file's content, so the same document still produces the same bytes on every run.
    /// </summary>
    public byte[]? DocumentId { get; init; }
}
