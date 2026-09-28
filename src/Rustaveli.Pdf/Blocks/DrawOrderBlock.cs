using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Sets the draw order of its child: content of a higher order is drawn over content of a lower one wherever it sits
/// on the page, and content of the same order in the order it comes. Content inside takes this order unless it sets
/// its own.
/// </summary>
internal sealed class DrawOrderBlock : EnclosingBlock
{
    public int Order { get; set; }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        // Pages are only drawn in order where some content asks for it; elsewhere, and while pages are only being
        // counted, there is nothing to reorder.
        if (context.Surface is not LayeredPageSink layers)
        {
            base.Render(availableSpace, context);
            return;
        }

        int outer = layers.Order;
        layers.Order = Order;

        try
        {
            base.Render(availableSpace, context);
        }
        finally
        {
            layers.Order = outer;
        }
    }
}
