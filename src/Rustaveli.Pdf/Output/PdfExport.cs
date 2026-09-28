using System.IO.Compression;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Output;
using Rustaveli.Pdf.Text;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf;

/// <summary>
/// Exports a document as PDF.
/// </summary>
/// <remarks>
/// <para>
/// The file is written in managed code: fonts are subset to the glyphs the document uses, images are embedded as
/// they were encoded where PDF can carry them so, and objects stream out as pages finish rather than accumulating
/// in memory. Documents can be exported in parallel; each export has its own writer and shares only the typefaces.
/// </para>
/// <para>
/// A document holds its layout progress while it is set, so one document must not be exported from two threads
/// at once. Export it once per thread, or compose one per thread.
/// </para>
/// </remarks>
public static class PdfExport
{
    /// <summary>Exports the document as PDF, returning the file's bytes.</summary>
    public static byte[] ExportPdf(this Document document, PdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        using MemoryStream stream = new MemoryStream();
        Export(document, stream, options);
        return stream.ToArray();
    }

    /// <summary>Exports the document as a PDF file at <paramref name="path"/>.</summary>
    /// <remarks>
    /// The file is written beside its destination and moved into place once complete, so a failed export leaves
    /// whatever was at <paramref name="path"/> untouched rather than a truncated document.
    /// </remarks>
    public static void ExportPdf(this Document document, string path, PdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(path);

        string target = Path.GetFullPath(path);
        string partial = target + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".partial";

        try
        {
            using (FileStream stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                Export(document, stream, options);

            if (File.Exists(target))
                File.Replace(partial, target, null);
            else
                File.Move(partial, target);
        }
        finally
        {
            if (File.Exists(partial))
                File.Delete(partial);
        }
    }

    /// <summary>Exports the document as PDF, writing it to <paramref name="stream"/>.</summary>
    /// <remarks>
    /// Pages are written as they are set, so a large document never has to fit in memory. The stream is not
    /// closed. If the export fails the stream holds part of a document; export to memory first where that matters.
    /// </remarks>
    public static void ExportPdf(this Document document, Stream stream, PdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanWrite)
            throw new ArgumentException("The stream cannot be written to.", nameof(stream));

        Export(document, stream, options);
    }

    private static void Export(Document document, Stream stream, PdfExportOptions? options)
    {
        TypeShaper shaper = (options?.Typefaces ?? TypefaceLibrary.Shared).Shaper;
        PdfWriterOptions writing = new PdfWriterOptions
        {
            CompressionLevel = options?.Compress == false ? CompressionLevel.NoCompression : CompressionLevel.Optimal,
        };

        using PdfDocumentWriter writer = new PdfDocumentWriter(stream, writing);
        CopyInfo(document.Info, writer.Info);

        using PdfSurface surface = new PdfSurface(writer, shaper);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(shaper);
        Typesetter.Render(document, surface, measurer);

        if (options?.RequireEveryGlyph == true && measurer.MissingCodepoints.Count > 0)
            throw new MissingGlyphException(measurer.MissingCodepoints);

        surface.Finish();
    }

    private static void CopyInfo(DocumentInfo info, PdfDocumentInfo target)
    {
        target.Title = info.Title;
        target.Author = info.Author;
        target.Subject = info.Subject;
        target.Keywords = info.Keywords;
        target.Creator = info.Creator;
        target.Producer = info.Producer;
        target.CreationDate = info.CreationDate;
        target.ModificationDate = info.ModificationDate;
    }
}
