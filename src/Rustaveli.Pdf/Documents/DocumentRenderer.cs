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
    public static void Render(Document document, IPageSink canvas, ITypeMeasurer measurer) =>
        Typesetter.Render(document, canvas, measurer);
}
