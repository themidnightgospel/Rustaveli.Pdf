using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Everything an element needs while drawing: the measurement services plus the surface to draw onto.
/// </summary>
public sealed class RenderContext(ISurface canvas, PlanContext layout)
{
    public ISurface Canvas { get; } = canvas;

    public PlanContext Layout { get; } = layout;

    public Pagination Page => Layout.Page;

    public ITypeMeasurer TextMeasurer => Layout.TextMeasurer;
}
