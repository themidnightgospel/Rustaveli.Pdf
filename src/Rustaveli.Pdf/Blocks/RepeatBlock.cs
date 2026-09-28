using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws its child again on every page its container continues onto, rather than once.
/// </summary>
/// <remarks>
/// A column of a row is ordinarily drawn until its content is used up and then left empty while its neighbours carry
/// on; repeated, it is drawn afresh beside them on each page, as a side label or a running rule is. Once drawn in
/// full, its content is reset, so text starts from its beginning again. It never keeps its container going by
/// itself: the container ends with the content that is not repeated.
/// </remarks>
internal sealed class RepeatBlock : EnclosingBlock
{
    internal override bool Repeats => true;

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Fit plan = Child.Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        Child.Render(availableSpace, context);

        // Only content drawn in full starts again: what has more to come continues on the next page as usual.
        if (plan.IsComplete)
            Child.ResetState(includeDocumentProgress: false);
    }
}
