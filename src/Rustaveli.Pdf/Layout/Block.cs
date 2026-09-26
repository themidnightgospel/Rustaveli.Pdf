using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// The base unit of a document tree: something that can report how much space it needs and then draw itself.
/// </summary>
/// <remarks>
/// Every element must honour one rule: <see cref="Block.Render" /> may only consume the space that <see cref="Block.Plan" />
/// promised for the same available space. The pagination engine measures first to decide what fits on the current
/// page, so an element that draws more than it measured will overflow silently.
/// </remarks>
public abstract class Block
{
    /// <summary>
    /// True for elements whose state describes progress through the document as a whole rather than through a
    /// single page, and so must survive the per-page reset applied to headers and footers.
    /// </summary>
    protected virtual bool TracksDocumentProgress => false;

    /// <summary>
    /// Reports what this element would do if given <paramref name="availableSpace" />, without drawing anything.
    /// Must not mutate state, because the engine measures speculatively and may discard the result.
    /// </summary>
    public abstract Fit Plan(Extent availableSpace, PlanContext context);

    /// <summary>
    /// Draws the element and advances any internal position so that a subsequent call continues where this one
    /// left off. Called at most once per page.
    /// </summary>
    public abstract void Render(Extent availableSpace, RenderContext context);

    /// <summary>Direct children, used for tree traversal. Null entries are skipped by callers.</summary>
    public virtual IEnumerable<Block?> GetChildren()
    {
        return Array.Empty<Block>();
    }

    /// <summary>
    /// Clears state accumulated while drawing so the tree can be rendered again from the beginning.
    /// Override in elements that remember how far they have progressed.
    /// </summary>
    protected virtual void ResetOwnState()
    {
    }

    /// <summary>
    /// Resets this element and everything beneath it.
    /// </summary>
    /// <param name="includeDocumentProgress">
    /// True when starting a whole new rendering pass, which clears everything. False when re-preparing a header
    /// or footer for the next page: pagination state is cleared so the band draws in full again, but elements
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

    /// <summary>Depth-first enumeration of this element and all of its descendants.</summary>
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
