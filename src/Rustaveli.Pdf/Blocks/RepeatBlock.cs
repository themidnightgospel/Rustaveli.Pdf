using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws its child afresh on every page its parent continues onto: a label beside a column that runs on for pages.
/// </summary>
internal sealed class RepeatBlock : EnclosingBlock
{
    /// <summary>Always, whatever the content: that is what this block is for.</summary>
    internal override bool Repeats => true;

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Fit plan = Child.Plan(availableSpace, context.Planning);

        if (!plan.PlacesContent)
            return;

        Child.Render(availableSpace, context);

        // Drawn whole, the content goes back to its beginning for the next page. Content still running on keeps its
        // place and continues as any other would.
        if (plan.IsComplete)
            Child.ResetState(includeDocumentProgress: false);
    }
}
