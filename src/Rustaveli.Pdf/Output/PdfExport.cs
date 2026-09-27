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
    private static readonly PdfName Lang = new PdfName("Lang");

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

    /// <summary>
    /// Exports the document to a PDF in the temporary folder and opens it in the viewer the system uses for PDFs, for
    /// a look while writing the document. Returns where the file was written.
    /// </summary>
    public static string ExportPdfAndOpen(this Document document, PdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        string path = Path.Combine(Path.GetTempPath(), $"{Name(document)}-{Guid.NewGuid().ToString("N").Substring(0, 8)}.pdf");
        document.ExportPdf(path, options);
        Open(path);
        return path;
    }

    /// <summary>Opens a file in the application the system uses for its kind.</summary>
    internal static Action<string> Open { get; set; } =
        path => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();

    /// <summary>A file name from the document's title, keeping only letters, digits and dashes; "document" without one.</summary>
    private static string Name(Document document)
    {
        string title = new string((document.Info.Title ?? string.Empty).Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
        return title.Length == 0 ? "document" : title.Substring(0, Math.Min(title.Length, 40));
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

        if (!string.IsNullOrWhiteSpace(document.Info.Language))
            writer.Catalog[Lang] = PdfString.FromText(document.Info.Language!.Trim());

        using PdfSurface surface = new PdfSurface(writer, shaper, options);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(shaper);
        Typesetter.Render(document, surface, measurer, options?.ImageResolution ?? 288);

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
