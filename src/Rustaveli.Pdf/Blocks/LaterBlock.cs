using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Composes its content only when layout first reaches it, and lets the content go once it is drawn in full, so that a
/// long document holds only the content of the pages still being set.
/// </summary>
internal sealed class LaterBlock : Block
{
    private Frame? _content;
    private bool _finished;

    /// <summary>Composes the content into the frame it is given.</summary>
    public required Action<IFrame> Compose { get; init; }

    /// <summary>
    /// Whether the content, once composed, is held for every later pass rather than let go and composed afresh.
    /// </summary>
    public bool Keep { get; init; }

    public override IEnumerable<Block?> GetChildren()
    {
        yield return _content;
    }

    /// <remarks>
    /// A new pass starts from the beginning. Content not kept is let go here too, so that each pass composes its own;
    /// kept content stays, and is reset with the rest of the tree beneath this block.
    /// </remarks>
    protected override void ResetOwnState()
    {
        _finished = false;

        if (!Keep)
            _content = null;
    }

    /// <remarks>
    /// The content's own progress is saved with the tree beneath, which includes the content held at the time; holding
    /// on to the reference here is what lets a restore find that content again after it was let go.
    /// </remarks>
    protected override object? SaveOwnProgress() => (_content, _finished);

    protected override void RestoreOwnProgress(object progress) =>
        (_content, _finished) = ((Frame?, bool))progress;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        _finished ? Fit.Nothing() : Content().Plan(availableSpace, context);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (_finished)
            return;

        Frame content = Content();
        Fit plan = content.Plan(availableSpace, context.Planning);

        if (plan.IsDeferred)
            return;

        content.Render(availableSpace, context);

        if (plan.IsComplete || plan.IsNothing)
        {
            _finished = true;

            if (!Keep)
                _content = null;
        }
    }

    private Frame Content()
    {
        if (_content is null)
        {
            _content = new Frame();
            Compose(_content);
        }

        return _content;
    }
}
