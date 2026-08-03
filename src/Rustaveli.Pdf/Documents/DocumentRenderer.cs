using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Documents;

/// <summary>
/// Entry point for rendering a composed document to any canvas.
/// </summary>
/// <remarks>
/// Backends call this rather than implementing pagination themselves, which keeps page-breaking behaviour
/// identical no matter what the output format is.
/// </remarks>
public static class DocumentRenderer
{
    public static void Render(Document document, IDocumentCanvas canvas, ITextMeasurer measurer) =>
        DocumentGenerator.Render(document, canvas, measurer);
}
