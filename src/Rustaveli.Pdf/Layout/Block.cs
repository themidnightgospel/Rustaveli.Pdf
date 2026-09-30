namespace Rustaveli.Pdf.Layout;

/// <summary>
/// The base unit of a document tree: something that can report how much space it needs and then draw itself.
/// </summary>
/// <remarks>
/// Every block must honour one rule: <see cref="Block.Render" /> may only consume the space that <see cref="Block.Plan" />
/// promised for the same available space. The pagination engine measures first to decide what fits on the current
/// page, so a block that draws more than it measured will overflow silently.
/// </remarks>
internal abstract class Block
{
    /// <summary>
    /// True for blocks whose state describes progress through the document as a whole rather than through a
    /// single page, and so must survive the per-page reset applied to headers and footers.
    /// </summary>
    protected virtual bool TracksDocumentProgress => false;

    /// <summary>
    /// True for content drawn again on every page its parent continues onto, rather than only until it is used
    /// up. Blocks whose children share a page, such as a row's columns, draw such content beside the rest.
    /// </summary>
    internal virtual bool Repeats => false;

    /// <summary>
    /// Reports what this block would do if given <paramref name="availableSpace" />, without drawing anything.
    /// Must not mutate state, because the engine measures speculatively and may discard the result.
    /// </summary>
    /// <remarks>
    /// While the context is tracing — to explain a layout failure — each measurement is recorded, nested within the one
    /// that asked for it.
    /// </remarks>
    public Fit Plan(Extent availableSpace, PlanContext context)
    {
        if (context.Trace is not { } trace)
            return PlanCore(availableSpace, context);

        PlanTrace.Node node = trace.Enter(this, availableSpace);

        try
        {
            Fit fit = PlanCore(availableSpace, context);
            node.Result = fit;
            return fit;
        }
        finally
        {
            trace.Leave(node);
        }
    }

    /// <summary>What <see cref="Plan"/> reports for this kind of block.</summary>
    protected abstract Fit PlanCore(Extent availableSpace, PlanContext context);

    /// <summary>
    /// Draws the block and advances any internal position so that a subsequent call continues where this one
    /// left off. Called at most once per page.
    /// </summary>
    public void Render(Extent availableSpace, RenderContext context)
    {
        if (context.Inspection is not { } inspection)
        {
            RenderCore(availableSpace, context);
            return;
        }

        LayoutInspection.Node node = inspection.Enter(this, context.Surface.Origin, availableSpace);

        try
        {
            RenderCore(availableSpace, context);
        }
        finally
        {
            inspection.Leave(node);
        }
    }

    /// <summary>What <see cref="Render"/> draws for this kind of block.</summary>
    protected abstract void RenderCore(Extent availableSpace, RenderContext context);

    /// <summary>
    /// Where in the code composing the document this block was made — a file and line — when that was being
    /// recorded, for a preview to lead back to; null otherwise.
    /// </summary>
    internal string? Source { get; } = SourceCapture.Current();

    /// <summary>Direct children, used for tree traversal. Null entries are skipped by callers.</summary>
    public virtual IEnumerable<Block?> GetChildren()
    {
        return Array.Empty<Block>();
    }

    /// <summary>
    /// Clears state accumulated while drawing so the tree can be rendered again from the beginning.
    /// Override in blocks that remember how far they have progressed.
    /// </summary>
    protected virtual void ResetOwnState()
    {
    }

    /// <summary>
    /// A copy of how far this block alone has progressed — what <see cref="ResetOwnState"/> clears — or null for
    /// a block that remembers nothing. Every block that overrides <see cref="ResetOwnState"/> overrides this.
    /// </summary>
    protected virtual object? SaveOwnProgress() => null;

    /// <summary>Returns this block to progress <see cref="SaveOwnProgress"/> copied.</summary>
    protected virtual void RestoreOwnProgress(object progress)
    {
    }

    /// <summary>
    /// How far this block and everything beneath it have progressed, so that layout can draw ahead to find where
    /// content would end and then return to where it was.
    /// </summary>
    internal Progress SaveProgress()
    {
        List<(Block Block, object Progress)> saved = [];

        foreach (Block block in Traverse())
        {
            if (block.SaveOwnProgress() is { } progress)
                saved.Add((block, progress));
        }

        return new Progress(saved);
    }

    /// <summary>Returns this block and everything beneath it to <paramref name="progress"/>.</summary>
    internal void RestoreProgress(Progress progress)
    {
        foreach ((Block block, object saved) in progress.Saved)
            block.RestoreOwnProgress(saved);
    }

    /// <summary>
    /// Resets this block and everything beneath it.
    /// </summary>
    /// <param name="includeDocumentProgress">
    /// True when starting a whole new rendering pass, which clears everything. False when re-preparing a header
    /// or footer for the next page: pagination state is cleared so the band draws in full again, but blocks
    /// counting document-wide occurrences keep what they have seen.
    /// </param>
    public void ResetState(bool includeDocumentProgress = true)
    {
        if (includeDocumentProgress || !TracksDocumentProgress)
        {
            ResetOwnState();
        }
        foreach (Block? child in GetChildren())
        {
            child?.ResetState(includeDocumentProgress);
        }
    }

    /// <summary>Depth-first enumeration of this block and all of its descendants.</summary>
    public IEnumerable<Block> Traverse()
    {
        yield return this;
        foreach (Block? child in GetChildren())
        {
            if (child == null)
            {
                continue;
            }
            foreach (Block item in child.Traverse())
            {
                yield return item;
            }
        }
    }
}
