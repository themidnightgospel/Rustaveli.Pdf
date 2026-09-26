using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Everything an element needs while drawing: the measurement services plus the surface to draw onto.
/// </summary>
public sealed class DrawContext(ICanvas canvas, LayoutContext layout)
{
    public ICanvas Canvas { get; } = canvas;

    public LayoutContext Layout { get; } = layout;

    public PageContext Page => Layout.Page;

    public ITextMeasurer TextMeasurer => Layout.TextMeasurer;
}
