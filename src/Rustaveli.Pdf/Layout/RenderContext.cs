using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Everything an element needs while drawing: the measurement services plus the surface to draw onto.
/// </summary>
internal sealed class RenderContext(ISurface canvas, PlanContext layout)
{
    public ISurface Surface { get; } = canvas;

    public PlanContext Planning { get; } = layout;

    public Pagination Pagination => Planning.Pagination;

    public ITypeMeasurer Measurer => Planning.Measurer;
}
