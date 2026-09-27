using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Tagging;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Everything an element needs while drawing: the measurement services, the surface to draw onto, and the structure
/// elements it is drawn inside.
/// </summary>
internal sealed class RenderContext(ISurface surface, PlanContext layout, StructureElement? structure = null)
{
    public ISurface Surface { get; } = surface;

    public PlanContext Planning { get; } = layout;

    public Pagination Pagination => Planning.Pagination;

    public ITypeMeasurer Measurer => Planning.Measurer;

    /// <summary>The structure being drawn inside, when the output is tagged.</summary>
    public TagStack Tags { get; } = new TagStack(surface, structure);
}
