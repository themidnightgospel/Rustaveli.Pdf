
namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// A surface that groups what is drawn on it into pages.
/// </summary>
internal interface IPageSink : ISurface, IDisposable
{
    void BeginPage(Extent size);

    void EndPage();
}
