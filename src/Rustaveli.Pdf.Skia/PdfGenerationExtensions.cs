using Rustaveli.Pdf.Documents;
using SkiaSharp;

namespace Rustaveli.Pdf.Skia;

/// <summary>
/// Renders composed documents to PDF.
/// </summary>
public static class PdfGenerationExtensions
{
    /// <summary>
    /// Serialises rendering across the process.
    /// </summary>
    /// <remarks>
    /// Skia's PDF backend does not tolerate concurrent document generation: two renders running at once produce
    /// files whose embedded font encodings disagree with their text operators, so the text extracts as garbage.
    /// This was reproduced with a separate <see cref="Skia.SkiaFontProvider" />, document and output stream per
    /// thread, which leaves Skia's own process-wide font and glyph caches as the only shared state — so it
    /// cannot be fixed from this side of the binding.
    ///
    /// Rendering is therefore serialised by default. Callers who have measured their own workload and want the
    /// throughput can opt out via <see cref="PdfGenerationOptions.AllowConcurrentRendering" />.
    /// </remarks>
    private static readonly object RenderGate = new object();

    public static byte[] GeneratePdf(this Document document, PdfGenerationOptions? options = null)
    {
        using MemoryStream memoryStream = new MemoryStream();
        document.GeneratePdf(memoryStream, options);
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
    public static void GeneratePdf(this Document document, string path, PdfGenerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(path, "path");
        byte[] bytes = document.GeneratePdf(options);
        File.WriteAllBytes(path, bytes);
    }

    public static void GeneratePdf(this Document document, Stream stream, PdfGenerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document, "document");
        ArgumentNullException.ThrowIfNull(stream, "stream");
        if (options == null)
        {
            options = new PdfGenerationOptions();
        }
        SkiaFontProvider fonts = options.Fonts ?? SkiaFontProvider.Shared;
        if (options.AllowConcurrentRendering)
        {
            RenderTo(stream, document, fonts, options);
            return;
        }
        lock (RenderGate)
        {
            RenderTo(stream, document, fonts, options);
        }
    }

    private static void RenderTo(Stream stream, Document document, SkiaFontProvider fonts, PdfGenerationOptions options)
    {
        SKDocumentPdfMetadata metadata = BuildMetadata(document.Metadata, options);
        using SKDocument sKDocument = SKDocument.CreatePdf(stream, metadata) ?? throw new InvalidOperationException("Skia could not create a PDF document for the supplied stream.");
        using SkiaPdfCanvas canvas = new SkiaPdfCanvas(sKDocument, fonts);
        DocumentRenderer.Render(document, canvas, new SkiaTextMeasurer(fonts));
        sKDocument.Close();
    }

    private static SKDocumentPdfMetadata BuildMetadata(DocumentMetadata metadata, PdfGenerationOptions options)
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
        if (metadata.CreationDate.HasValue)
        {
            result.Creation = metadata.CreationDate.Value.UtcDateTime;
        }
        if (metadata.ModificationDate.HasValue)
        {
            result.Modified = metadata.ModificationDate.Value.UtcDateTime;
        }
        return result;
    }
}
