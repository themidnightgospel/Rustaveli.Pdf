using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Composes its content only when layout first reaches it, and lets it go once it is drawn in full, so a very large
/// document holds only the content of the pages being drawn.
/// </summary>
/// <remarks>
/// Kept, the content is composed once and held for every later pass instead. Let go, it is composed afresh in each
/// pass over the document, as the page count is settled.
/// </remarks>
internal sealed class LaterBlock : Block
{
    private Frame? _content;
    private bool _done;

    public required Action<IFrame> Compose { get; init; }

    /// <summary>Whether the content is kept once composed rather than let go when drawn.</summary>
    public bool Keep { get; init; }

    public override IEnumerable<Block?> GetChildren()
    {
        yield return _content;
    }

    protected override void ResetOwnState()
    {
        _done = false;

        if (!Keep)
            _content = null;
    }

    // The content itself is saved as the tree beneath is; here only whether it is held, and whether it is done.
    protected override object? SaveOwnProgress() => (_content, _done);

    protected override void RestoreOwnProgress(object progress) => (_content, _done) = ((Frame?, bool))progress;

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        _done ? Fit.Nothing() : Content().Plan(availableSpace, context);

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (_done)
            return;

        Frame content = Content();
        Fit plan = content.Plan(availableSpace, context.Planning);

        if (plan.IsDeferred)
            return;

        content.Render(availableSpace, context);

        if (!plan.IsComplete && !plan.IsNothing)
            return;

        _done = true;

        if (!Keep)
            _content = null;
    }

    private Frame Content()
    {
        if (_content is null)
        {
            Frame frame = new Frame();
            Compose(frame);
            _content = frame;
        }

        return _content;
    }
}
