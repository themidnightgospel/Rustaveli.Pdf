using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Skia;
using SkiaSharp;

namespace Rustaveli.Pdf;

/// <summary>
/// Renders composed documents to PDF.
/// </summary>
public static class PdfExport
{
    /// <summary>
    /// Serialises rendering across the process.
    /// </summary>
    /// <remarks>
    /// Skia's PDF backend does not tolerate concurrent document generation: two renders running at once produce
    /// files whose embedded font encodings disagree with their text operators, so the text extracts as garbage.
    /// This was reproduced with a separate <see cref="SkiaFontProvider" />, document and output stream per
    /// thread, which leaves Skia's own process-wide font and glyph caches as the only shared state — so it
    /// cannot be fixed from this side of the binding.
    ///
    /// Rendering is therefore serialised by default. Callers who have measured their own workload and want the
    /// throughput can opt out via <see cref="PdfExportOptions.AllowConcurrentRendering" />.
    /// </remarks>
    private static readonly object RenderGate = new object();

    public static byte[] ExportPdf(this Document document, PdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document, "document");
        if (options == null)
        {
            options = new PdfExportOptions();
        }
        using MemoryStream memoryStream = new MemoryStream();
        SkiaFontProvider fonts = options.Fonts ?? SkiaFontProvider.Shared;
        if (options.AllowConcurrentRendering)
        {
            RenderTo(memoryStream, document, fonts, options);
        }
        else
        {
            lock (RenderGate)
            {
                RenderTo(memoryStream, document, fonts, options);
            }
        }
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Renders the document to a file, writing it only once generation has fully succeeded.
    /// </summary>
    /// <remarks>
    /// Rendering straight into the file would leave a structurally valid but silently truncated PDF behind if
    /// anything failed part-way — Skia closes the document as it unwinds, so the result looks complete. Building
    /// the whole file first means a failed render leaves the target untouched.
    /// </remarks>
    public static void ExportPdf(this Document document, string path, PdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path, "path");
        byte[] bytes = document.ExportPdf(options);
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// Renders the document to a stream, writing it only once generation has fully succeeded.
    /// </summary>
    /// <remarks>
    /// Skia writes its output through callbacks from native code, where a managed exception cannot unwind. Handed
    /// the caller's stream directly, one that threw — because it cannot report a Position, as response, network
    /// and compression streams cannot, or because a write failed — crashed or hung the process instead of failing
    /// the call. Rendering into memory first keeps every such failure an ordinary exception, and means a failed
    /// render writes nothing rather than a truncated document.
    /// </remarks>
    public static void ExportPdf(this Document document, Stream stream, PdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document, "document");
        ArgumentNullException.ThrowIfNull(stream, "stream");
        byte[] bytes = document.ExportPdf(options);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void RenderTo(Stream stream, Document document, SkiaFontProvider fonts, PdfExportOptions options)
    {
        SKDocumentPdfMetadata metadata = BuildMetadata(document.Info, options);
        using SKDocument sKDocument = SKDocument.CreatePdf(stream, metadata) ?? throw new InvalidOperationException("Skia could not create a PDF document for the supplied stream.");
        using SkiaPdfSurface canvas = new SkiaPdfSurface(sKDocument, fonts);
        Typesetter.Render(document, canvas, new SkiaTypeMeasurer(fonts));
        sKDocument.Close();
    }

    private static SKDocumentPdfMetadata BuildMetadata(DocumentInfo metadata, PdfExportOptions options)
    {
        SKDocumentPdfMetadata result = new SKDocumentPdfMetadata
        {
            RasterDpi = options.RasterDpi,
            PdfA = options.PdfA,
            EncodingQuality = options.EncodingQuality
        };
        if (metadata.Title != null)
        {
            result.Title = metadata.Title;
        }
        if (metadata.Author != null)
        {
            result.Author = metadata.Author;
        }
        if (metadata.Subject != null)
        {
            result.Subject = metadata.Subject;
        }
        if (metadata.Keywords != null)
        {
            result.Keywords = metadata.Keywords;
        }
        if (metadata.Creator != null)
        {
            result.Creator = metadata.Creator;
        }
        if (metadata.Producer != null)
        {
            result.Producer = metadata.Producer;
        }
        // SkiaSharp stamps the machine's own UTC offset onto whatever clock reading it is given, so the reading has
        // to be local time. Handed UTC, every date came out shifted by the local offset on any machine not on UTC.
        if (metadata.CreationDate.HasValue)
        {
            result.Creation = metadata.CreationDate.Value.LocalDateTime;
        }
        if (metadata.ModificationDate.HasValue)
        {
            result.Modified = metadata.ModificationDate.Value.LocalDateTime;
        }
        return result;
    }
}
