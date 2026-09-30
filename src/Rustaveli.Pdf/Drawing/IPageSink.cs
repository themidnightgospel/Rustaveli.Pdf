namespace Rustaveli.Pdf.Drawing;

/// <summary>
/// A surface that gathers what is drawn into pages. Drawing happens only between <see cref="BeginPage"/> and the
/// <see cref="EndPage"/> after it, and pages never nest.
/// </summary>
internal interface IPageSink : ISurface, IDisposable
{
    /// <summary>
    /// Starts a page of <paramref name="size"/>, drawn from scratch: no transform, nothing saved and nothing clipped.
    /// </summary>
    void BeginPage(Extent size);

    /// <summary>
    /// Finishes the page begun last. A sink that holds drawings back, to draw them in another order, draws them here.
    /// </summary>
    void EndPage();
}
