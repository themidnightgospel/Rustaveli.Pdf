using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Records where its content is drawn, once for every page it is drawn on, under a name that content composed per
/// page can look up.
/// </summary>
internal sealed class CaptureBlock : EnclosingBlock
{
    public required string Name { get; init; }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        Fit plan = Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        if (!context.DrawsAhead)
            context.Pagination.RegisterPosition(Name, new CapturedPosition(context.Pagination.Folio, context.Surface.Origin, availableSpace));

        base.RenderCore(availableSpace, context);
    }
}
