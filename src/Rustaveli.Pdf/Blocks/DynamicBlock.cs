using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Content composed afresh for every page it reaches, from the state it had got to, drawn as far as it fits.
/// </summary>
/// <remarks>
/// A page is measured more than once before it is drawn, so the content composed for it is kept, with the page and
/// room it was composed for, and composed again only when they change.
/// </remarks>
internal sealed class DynamicBlock<TState>(IDynamicContent<TState> content) : Block
{
    private TState _state = content.Initial;
    private bool _done;
    private Composed? _composed;

    protected override void ResetOwnState()
    {
        _state = content.Initial;
        _done = false;
        _composed = null;
    }

    protected override object? SaveOwnProgress() => (_state, _done);

    protected override void RestoreOwnProgress(object progress)
    {
        (_state, _done) = ((TState, bool))progress;
        _composed = null;
    }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (_done)
            return Fit.Nothing();

        Composed composed = Compose(availableSpace, context);
        Fit plan = composed.Frame.Plan(availableSpace, context);

        if (plan.IsDeferred)
            return plan;

        Extent size = plan.IsNothing ? Extent.Zero : plan.Size;
        return composed.Part.HasMore ? Fit.Partial(size) : Fit.Complete(size);
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (_done)
            return;

        Composed composed = Compose(availableSpace, context.Planning);

        if (composed.Frame.Plan(availableSpace, context.Planning).IsDeferred)
            return;

        composed.Frame.Render(availableSpace, context);
        _state = composed.Part.Next;
        _done = !composed.Part.HasMore;
        _composed = null;
    }

    private Composed Compose(Extent room, PlanContext context)
    {
        Pagination pagination = context.Pagination;

        if (_composed is { } kept
            && kept.Folio == pagination.Folio
            && kept.CountKnown == pagination.IsPageCountKnown
            && kept.Room == room)
        {
            return kept;
        }

        DynamicPart<TState> part = content.Compose(new DynamicPage(context, room), _state)
            ?? throw new InvalidOperationException($"{content.GetType().Name}.Compose returned no part for page {pagination.Folio}.");

        Frame frame = new Frame();
        part.Content(frame);

        _composed = new Composed(pagination.Folio, pagination.IsPageCountKnown, room, part, frame);
        return _composed;
    }

    /// <summary>What was composed for a page, and the page and room it was composed for.</summary>
    private sealed record Composed(int Folio, bool CountKnown, Extent Room, DynamicPart<TState> Part, Frame Frame);
}
