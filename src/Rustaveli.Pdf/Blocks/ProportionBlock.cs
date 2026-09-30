using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Gives its child a box of a fixed ratio of width to height, as large as the room allows along the dimension chosen,
/// and takes exactly that box whatever the child needs of it.
/// </summary>
internal sealed class ProportionBlock : EnclosingBlock
{
    /// <summary>Width divided by height; one, a square, unless set.</summary>
    public float Ratio { get; set; } = 1f;

    /// <summary>Which dimension of the room the box is fitted to; the width unless set.</summary>
    public ProportionFit Fit { get; set; } = ProportionFit.Width;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (Ratio <= 0)
            return Layout.Fit.Defer("The proportion must be greater than zero.");

        Extent box = Box(availableSpace);

        if (!box.FitsIn(availableSpace))
            return Layout.Fit.Defer("The space available is too small for the requested proportion.");

        return Resized(base.PlanCore(box, context), box);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null || Ratio <= 0)
            return;

        // The box starts at the top left, right to left as well: the box is the whole of what this block takes.
        Child.Render(Box(availableSpace), context);
    }

    private Extent Box(Extent room)
    {
        Extent acrossTheWidth = new Extent(room.Width, room.Width / Ratio);
        Extent downTheHeight = new Extent(room.Height * Ratio, room.Height);

        return Fit switch
        {
            ProportionFit.Height => downTheHeight,

            // The larger box that fits: the full width, unless that runs past the bottom of the room.
            ProportionFit.Area => acrossTheWidth.Height <= room.Height + Extent.Epsilon ? acrossTheWidth : downTheHeight,
            _ => acrossTheWidth,
        };
    }
}
