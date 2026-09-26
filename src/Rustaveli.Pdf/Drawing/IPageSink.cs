
namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// A canvas that groups drawing operations into pages.
/// </summary>
internal interface IPageSink : ISurface, IDisposable
{
    void BeginPage(Extent size);

    void EndPage();
}
