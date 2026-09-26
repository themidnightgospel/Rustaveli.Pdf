using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// A canvas that groups drawing operations into pages.
/// </summary>
public interface IDocumentCanvas : ICanvas, IDisposable
{
    void BeginPage(Size size);

    void EndPage();
}
